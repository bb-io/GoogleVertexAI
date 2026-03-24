using System.Globalization;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using RestSharp;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("File search")]
public class FileSearchActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiInvocable(invocationContext)
{
    private const int OperationPollingDelayMs = 5000;
    private const int MaxOperationPollAttempts = 24;

    [Action("Create file search store", Description = "Create a Gemini File Search store.")]
    public async Task<FileSearchStoreResponse> CreateFileSearchStore([ActionParameter] CreateFileSearchStoreRequest input)
    {
        var request = GeminiApiClient.CreateRequest("v1beta/fileSearchStores", Method.Post);
        request.AddJsonBody(new { displayName = input.DisplayName });

        var response = await GeminiApiClient.ExecuteAsync<GeminiFileSearchStoreResource>(request);

        return new FileSearchStoreResponse
        {
            StoreName = response.Name ?? string.Empty,
            DisplayName = response.DisplayName
        };
    }

    [Action("Upload file to store", Description = "Upload a Blackbird file directly into a Gemini File Search store and wait until indexing completes.")]
    public async Task<FileSearchUploadResponse> UploadFileToStore([ActionParameter] UploadFileToStoreRequest input)
    {
        await using var stream = await fileManagementClient.DownloadAsync(input.File);
        var displayName = input.DisplayName ?? input.File.Name;

        if (IsGeminiApiKeyConnection())
        {
            return await UploadFileToStoreWithApiKey(input, stream, displayName);
        }

        EnsureVertexAiConnection();
        stream.Position = 0;

        var effectiveRegion = ResolveVertexRegion();
        var bucketName = await EnsureRegionalBucketAsync(Storage!, ProjectId, effectiveRegion);
        var objectName = $"file-search/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}/{Path.GetFileName(input.File.Name)}";
        var contentType = input.File.ContentType ?? "application/octet-stream";

        await Storage!.UploadObjectAsync(bucketName, objectName, contentType, stream);

        var registerRequest = GeminiApiClient.CreateRequest("v1beta/files:register", Method.Post);
        registerRequest.AddJsonBody(new GeminiRegisterFilesRequest
        {
            Uris = [$"gs://{bucketName}/{objectName}"]
        });

        var registerResponse = await GeminiApiClient.ExecuteAsync<GeminiRegisterFilesResponse>(registerRequest);
        var fileName = NormalizeFileName(registerResponse.Files?.FirstOrDefault()?.Name);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new PluginApplicationException("Gemini file registration did not return a file name.");
        }

        await Task.Delay(OperationPollingDelayMs);

        var importRequest = GeminiApiClient.CreateRequest($"v1beta/{input.StoreName}:importFile", Method.Post);
        importRequest.AddJsonBody(new GeminiImportFileToSearchStoreRequest
        {
            FileName = fileName,
            CustomMetadata = BuildCustomMetadata(input.CustomMetadata),
            ChunkingConfig = BuildChunkingConfig(input.MaxTokensPerChunk, input.MaxOverlapTokens)
        });

        var importResponse = await GeminiApiClient.ExecuteAsync<GeminiFileSearchOperation>(importRequest);
        if (string.IsNullOrWhiteSpace(importResponse.Name))
        {
            throw new PluginApplicationException("Gemini File Search import did not return an operation name.");
        }

        await WaitForOperationAsync(importResponse.Name);

        return new FileSearchUploadResponse
        {
            StoreName = input.StoreName,
            OperationName = importResponse.Name,
            DisplayName = displayName
        };
    }

    private async Task<FileSearchUploadResponse> UploadFileToStoreWithApiKey(
        UploadFileToStoreRequest input,
        Stream stream,
        string displayName)
    {
        stream.Position = 0;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var startRequest = GeminiApiClient.CreateRequest(
            $"upload/v1beta/{input.StoreName}:uploadToFileSearchStore",
            Method.Post);

        startRequest.AddHeader("X-Goog-Upload-Protocol", "resumable");
        startRequest.AddHeader("X-Goog-Upload-Command", "start");
        startRequest.AddHeader("X-Goog-Upload-Header-Content-Length", fileBytes.Length.ToString(CultureInfo.InvariantCulture));
        startRequest.AddHeader("X-Goog-Upload-Header-Content-Type", input.File.ContentType ?? "application/octet-stream");
        startRequest.AddJsonBody(new GeminiUploadToFileSearchStoreRequest
        {
            DisplayName = displayName,
            MimeType = input.File.ContentType,
            CustomMetadata = BuildCustomMetadata(input.CustomMetadata),
            ChunkingConfig = BuildChunkingConfig(input.MaxTokensPerChunk, input.MaxOverlapTokens)
        });

        var startResponse = await GeminiApiClient.ExecuteAsync(startRequest);
        var uploadUrl = startResponse.Headers?
            .FirstOrDefault(x => x.Name?.Equals("X-Goog-Upload-URL", StringComparison.OrdinalIgnoreCase) == true)?
            .Value?.ToString();

        if (string.IsNullOrWhiteSpace(uploadUrl))
        {
            throw new PluginApplicationException("Gemini File Search upload did not return a resumable upload URL.");
        }

        var uploadRequest = GeminiApiClient.CreateAbsoluteRequest(uploadUrl, Method.Post);
        uploadRequest.AddHeader("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.AddHeader("X-Goog-Upload-Offset", "0");
        uploadRequest.AddHeader("Content-Length", fileBytes.Length.ToString(CultureInfo.InvariantCulture));
        uploadRequest.AddParameter(input.File.ContentType ?? "application/octet-stream", fileBytes, ParameterType.RequestBody);

        var uploadResponse = await GeminiApiClient.ExecuteAsync<GeminiFileSearchOperation>(uploadRequest);
        if (string.IsNullOrWhiteSpace(uploadResponse.Name))
        {
            throw new PluginApplicationException("Gemini File Search upload did not return an operation name.");
        }

        await WaitForOperationAsync(uploadResponse.Name);

        return new FileSearchUploadResponse
        {
            StoreName = input.StoreName,
            OperationName = uploadResponse.Name,
            DisplayName = displayName
        };
    }

    [Action("Delete file search store", Description = "Delete a Gemini File Search store.")]
    public async Task<FileSearchStoreResponse> DeleteFileSearchStore([ActionParameter] DeleteFileSearchStoreRequest input)
    {
        var request = GeminiApiClient.CreateRequest(
            $"v1beta/{input.StoreName}?force={(input.Force ?? true).ToString().ToLowerInvariant()}",
            Method.Delete);

        await GeminiApiClient.ExecuteAsync(request);

        return new FileSearchStoreResponse
        {
            StoreName = input.StoreName
        };
    }

    [Action("Search documents", Description = "Search indexed documents in one or more Gemini File Search stores and return a grounded answer. This action requires a Gemini API key connection.")]
    public async Task<GeneratedTextResponse> SearchDocuments(
        [ActionParameter] SearchDocumentsRequest input,
        [ActionParameter] PromptRequest promptRequest)
    {
        EnsureGeminiApiKeyConnection("Searching across File Search stores");

        var fileSearchResponse = await ExecuteFileSearchGenerateAsync(
            input.AIModel,
            input.Query,
            promptRequest,
            input.FileSearchStoreNames,
            input.MetadataFilter);

        return new GeneratedTextResponse
        {
            GeneratedText = ExtractText(fileSearchResponse),
            Usage = ExtractUsage(fileSearchResponse),
            RetrievedContexts = ExtractRetrievedContexts(fileSearchResponse)
        };
    }

    private async Task<GeminiGenerateContentResponse> ExecuteFileSearchGenerateAsync(
        string modelId,
        string prompt,
        PromptRequest promptRequest,
        IEnumerable<string> fileSearchStoreNames,
        string? metadataFilter)
    {
        var request = GeminiApiClient.CreateRequest($"v1beta/models/{modelId}:generateContent", Method.Post);
        request.AddJsonBody(new GeminiGenerateContentRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts =
                    [
                        new GeminiPart { Text = prompt }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = promptRequest.Temperature ?? 0.9f,
                TopP = promptRequest.TopP ?? 1.0f,
                TopK = promptRequest.TopK ?? 3,
                MaxOutputTokens = promptRequest.MaxOutputTokens ?? ModelTokenService.GetMaxTokensForModel(modelId)
            },
            SafetySettings = BuildSafetySettings(promptRequest),
            Tools = BuildTools(fileSearchStoreNames, metadataFilter)
        });

        return await GeminiApiClient.ExecuteAsync<GeminiGenerateContentResponse>(request);
    }

    private async Task WaitForOperationAsync(string operationName)
    {
        for (var attempt = 0; attempt < MaxOperationPollAttempts; attempt++)
        {
            var request = GeminiApiClient.CreateRequest($"v1beta/{operationName}", Method.Get);
            var operation = await GeminiApiClient.ExecuteAsync<GeminiFileSearchOperation>(request);

            if (operation.Done)
            {
                if (operation.Error is not null)
                {
                    throw new PluginApplicationException(operation.Error.Message ?? "Gemini File Search operation failed.");
                }

                return;
            }

            await Task.Delay(OperationPollingDelayMs);
        }

        throw new PluginApplicationException("Gemini File Search operation did not complete in time.");
    }

    private static string ExtractText(GeminiGenerateContentResponse response)
    {
        var candidate = response.Candidates?.FirstOrDefault();
        return string.Concat(candidate?.Content?.Parts?
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text) ?? []);
    }

    private static UsageDto ExtractUsage(GeminiGenerateContentResponse response)
    {
        return response.UsageMetadata is null
            ? new UsageDto()
            : new UsageDto(
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount,
                response.UsageMetadata.TotalTokenCount);
    }

    private static List<RetrievedContextDto> ExtractRetrievedContexts(GeminiGenerateContentResponse response)
    {
        return response.Candidates?.FirstOrDefault()?.GroundingMetadata?.GroundingChunks?
            .Select(x => x.RetrievedContext)
            .Where(x => x is not null)
            .Select(x => new RetrievedContextDto
            {
                Title = x!.Title,
                Uri = x.Uri,
                Text = x.Text,
                FileSearchStore = x.FileSearchStore
            })
            .ToList() ?? [];
    }

    private static List<GeminiTool>? BuildTools(IEnumerable<string> fileSearchStoreNames, string? metadataFilter)
    {
        var stores = fileSearchStoreNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stores.Count == 0)
        {
            return null;
        }

        return
        [
            new GeminiTool
            {
                FileSearch = new GeminiFileSearchTool
                {
                    FileSearchStoreNames = stores,
                    MetadataFilter = string.IsNullOrWhiteSpace(metadataFilter) ? null : metadataFilter
                }
            }
        ];
    }

    private static List<GeminiSafetySetting>? BuildSafetySettings(PromptRequest promptRequest)
    {
        if (promptRequest is not { SafetyCategories: not null, SafetyCategoryThresholds: not null })
        {
            return null;
        }

        return promptRequest.SafetyCategories
            .Take(Math.Min(promptRequest.SafetyCategories.Count(), promptRequest.SafetyCategoryThresholds.Count()))
            .Zip(promptRequest.SafetyCategoryThresholds,
                (category, threshold) => new GeminiSafetySetting
                {
                    Category = MapSafetyCategory(category),
                    Threshold = MapSafetyThreshold(threshold)
                })
            .ToList();
    }

    private static string MapSafetyCategory(string category)
    {
        return category switch
        {
            "SexuallyExplicit" => "HARM_CATEGORY_SEXUALLY_EXPLICIT",
            "HateSpeech" => "HARM_CATEGORY_HATE_SPEECH",
            "Harassment" => "HARM_CATEGORY_HARASSMENT",
            "DangerousContent" => "HARM_CATEGORY_DANGEROUS_CONTENT",
            _ => throw new PluginApplicationException($"Unsupported safety category: {category}")
        };
    }

    private static string MapSafetyThreshold(string threshold)
    {
        return threshold switch
        {
            "BlockNone" => "BLOCK_NONE",
            "BlockLowAndAbove" => "BLOCK_LOW_AND_ABOVE",
            "BlockMediumAndAbove" => "BLOCK_MEDIUM_AND_ABOVE",
            "BlockOnlyHigh" => "BLOCK_ONLY_HIGH",
            _ => throw new PluginApplicationException($"Unsupported safety threshold: {threshold}")
        };
    }

    private static List<GeminiCustomMetadata>? BuildCustomMetadata(IEnumerable<string>? customMetadata)
    {
        if (customMetadata is null)
        {
            return null;
        }

        var metadata = new List<GeminiCustomMetadata>();
        foreach (var entry in customMetadata.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == entry.Length - 1)
            {
                throw new PluginApplicationException("Custom metadata must use key=value format.");
            }

            var key = entry[..separatorIndex].Trim();
            var value = entry[(separatorIndex + 1)..].Trim();

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
            {
                metadata.Add(new GeminiCustomMetadata
                {
                    Key = key,
                    NumericValue = numericValue
                });
            }
            else
            {
                metadata.Add(new GeminiCustomMetadata
                {
                    Key = key,
                    StringValue = value.Trim('"')
                });
            }
        }

        return metadata.Count == 0 ? null : metadata;
    }

    private static GeminiChunkingConfig? BuildChunkingConfig(int? maxTokensPerChunk, int? maxOverlapTokens)
    {
        if (!maxTokensPerChunk.HasValue && !maxOverlapTokens.HasValue)
        {
            return null;
        }

        return new GeminiChunkingConfig
        {
            WhiteSpaceConfig = new GeminiWhiteSpaceChunkingConfig
            {
                MaxTokensPerChunk = maxTokensPerChunk,
                MaxOverlapTokens = maxOverlapTokens ?? 0
            }
        };
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return fileName.StartsWith("files/", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"files/{fileName}";
    }
}
