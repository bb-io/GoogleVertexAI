using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Dto;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Transformations;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.GoogleVertexAI.Actions;

[ActionList("Images")]
public class ImageActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : VertexAiInvocable(invocationContext)
{
    [Action("Extract localizable content from image", Description = "Extracts localizable text from an image into an XLIFF file.")]
    public async Task<ExtractLocalizableContentFromImageResponse> ExtractLocalizableContentFromImage(
        [ActionParameter] AIModelRequest model,
        [ActionParameter] ExtractLocalizableContentFromImageRequest input)
    {
        var sourceLanguageInstruction = string.IsNullOrWhiteSpace(input.SourceLanguage)
            ? "Detect the dominant language of the extracted text and return its BCP-47 language code."
            : $"The source language is '{input.SourceLanguage}'. Return this exact value in source_language.";

        var prompt = "Perform OCR on the supplied image and extract all visible text that should be localized. " +
                     "Return each complete UI text item as a separate segment in natural reading order. " +
                     "Preserve the exact source spelling, punctuation, capitalization, numbers, and meaningful line breaks. " +
                     "Do not translate, summarize, correct, merge unrelated text, or describe visual elements. " +
                     "If the image contains no localizable text, return an empty segments array and use 'und' as the source language unless a source language was supplied. " +
                     sourceLanguageInstruction;

        Transformation transformation;
        await using (var imageStream = await fileManagementClient.DownloadAsync(input.Image))
        {
            var loadResult = Transformation.Load(imageStream, input.Image.Name, input.Image.ContentType);
            if (!loadResult.Success)
            {
                throw new PluginMisconfigurationException(
                    $"The original image could not be embedded in the XLIFF file: {loadResult.Error}");
            }

            transformation = loadResult.Value!;
        }

        if (string.IsNullOrWhiteSpace(transformation.Original)
            || string.IsNullOrWhiteSpace(transformation.OriginalMediaType))
        {
            throw new PluginMisconfigurationException(
                "The original image could not be embedded in the XLIFF file.");
        }

        var response = await ExecuteMultimodalRequest(
            model.AIModel,
            [
                new GeminiPart { Text = prompt },
                new GeminiPart
                {
                    InlineData = new GeminiInlineData
                    {
                        MimeType = transformation.OriginalMediaType,
                        Data = transformation.Original
                    }
                }
            ],
            new GeminiGenerationConfig
            {
                ResponseSchema = ResponseSchemas.ImageLocalizationExtraction,
                ResponseMimeType = "application/json",
                Temperature = 0,
                TopP = 1,
                TopK = 1,
                MaxOutputTokens = 8192
            });

        var extraction = DeserializeExtraction(ExtractText(response));
        var segments = extraction.Segments
            .Select(x => x.Text)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        var sourceLanguage = !string.IsNullOrWhiteSpace(input.SourceLanguage)
            ? input.SourceLanguage
            : extraction.SourceLanguage?.Trim();

        if (segments.Count == 0 && string.IsNullOrWhiteSpace(sourceLanguage))
        {
            sourceLanguage = "und";
        }

        if (segments.Count > 0 && string.IsNullOrWhiteSpace(sourceLanguage))
        {
            throw new PluginApplicationException("Gemini extracted text but did not return its source language.");
        }

        var outputName = BuildOutputName(input.OutputFileName, input.Image.Name, ".xlf");
        transformation.SourceLanguage = sourceLanguage;
        transformation.BilingualFileName = outputName;

        foreach (var text in segments)
        {
            transformation.AddTranslationUnit(text);
        }

        using var outputStream = transformation.ToStream();
        var file = await fileManagementClient.UploadAsync(outputStream, MediaTypes.Xliff2, outputName);

        return new ExtractLocalizableContentFromImageResponse
        {
            File = file,
            SourceLanguage = sourceLanguage,
            SegmentsCount = segments.Count,
            Usage = ExtractUsage(response)
        };
    }

    [Action("Generate localized image variant", Description = "Generates a localized image using the original image and translations embedded in an XLIFF file.")]
    public async Task<ImageResponse> GenerateLocalizedImageVariant(
        [ActionParameter] ImageGenerationModelRequest model,
        [ActionParameter] GenerateLocalizedImageVariantRequest input)
    {
        var extension = Path.GetExtension(input.LocalizedFile.Name);
        if (!extension.Equals(".xlf", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".xliff", StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginMisconfigurationException(
                "The localized file must be an XLIFF file with an .xlf or .xliff extension.");
        }

        Transformation transformation;
        await using (var localizedStream = await fileManagementClient.DownloadAsync(input.LocalizedFile))
        {
            var loadResult = Transformation.Load(
                localizedStream,
                input.LocalizedFile.Name,
                input.LocalizedFile.ContentType);

            if (!loadResult.Success)
            {
                throw new PluginMisconfigurationException($"The localized file could not be read: {loadResult.Error}");
            }

            transformation = loadResult.Value!;
        }

        var replacements = transformation.GetUnits()
            .SelectMany(unit => unit.Segments)
            .Select((segment, index) => new
            {
                order = index + 1,
                source = segment.GetSource(),
                target = segment.GetTarget()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.source) && !string.IsNullOrWhiteSpace(x.target))
            .ToList();

        if (replacements.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "The localized XLIFF file does not contain any segments with both source and target text.");
        }

        var sourceResult = transformation.Source();
        if (!sourceResult.Success)
        {
            throw new PluginMisconfigurationException(
                $"The original image could not be restored from the localized XLIFF file: {sourceResult.Error}");
        }

        var source = sourceResult.Value!;
        byte[] sourceBytes;
        using (var sourceStream = source.ToStream())
        {
            sourceBytes = await sourceStream.GetByteData();
        }

        var prompt = BuildLocalizationPrompt(
            JsonConvert.SerializeObject(replacements, Formatting.Indented),
            transformation.SourceLanguage,
            transformation.TargetLanguage,
            input.AdditionalInstructions);

        var response = await ExecuteMultimodalRequest(
            model.AIModel,
            [
                new GeminiPart { Text = prompt },
                new GeminiPart
                {
                    InlineData = new GeminiInlineData
                    {
                        MimeType = source.OriginalMediaType,
                        Data = Convert.ToBase64String(sourceBytes)
                    }
                }
            ],
            new GeminiGenerationConfig
            {
                ResponseModalities = ["TEXT", "IMAGE"]
            });

        var image = response.Candidates?
            .SelectMany(x => x.Content?.Parts ?? [])
            .Select(x => x.InlineData)
            .FirstOrDefault(x => x is not null
                                 && x.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrWhiteSpace(x.Data));

        if (image is null)
        {
            var modelMessage = ExtractText(response);
            var suffix = string.IsNullOrWhiteSpace(modelMessage) ? string.Empty : $" Gemini response: {modelMessage}";
            throw new PluginApplicationException($"Gemini did not return a generated image.{suffix}");
        }

        byte[] outputBytes;
        try
        {
            outputBytes = Convert.FromBase64String(image.Data);
        }
        catch (FormatException exception)
        {
            throw new PluginApplicationException("Gemini returned invalid base64 image data.", exception);
        }

        using var outputStream = new MemoryStream(outputBytes);
        var outputExtension = ExtensionForMediaType(image.MimeType);
        var outputName = BuildOutputName(input.OutputImageName, source.OriginalName, outputExtension);
        var outputFile = await fileManagementClient.UploadAsync(outputStream, image.MimeType, outputName);

        return new ImageResponse { Image = outputFile, Usage = ExtractUsage(response) };
    }

    private async Task<GeminiGenerateContentResponse> ExecuteMultimodalRequest(
        string model,
        List<GeminiPart> parts,
        GeminiGenerationConfig generationConfig,
        CancellationToken cancellationToken = default)
    {
        var request = IsGeminiApiKeyConnection()
            ? GeminiApiClient.CreateRequest($"v1beta/models/{model}:generateContent", Method.Post)
            : GeminiApiClient.CreateAbsoluteRequest(BuildVertexGenerateContentUrl(model), Method.Post);

        request.AddJsonBody(new GeminiGenerateContentRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts = parts
                }
            ],
            GenerationConfig = generationConfig
        });

        return await GeminiApiClient.ExecuteAsync<GeminiGenerateContentResponse>(request, cancellationToken);
    }

    private string BuildVertexGenerateContentUrl(string model)
    {
        var location = string.IsNullOrWhiteSpace(Region) ? "global" : Region.Trim().ToLowerInvariant();
        var host = location == "global"
            ? "aiplatform.googleapis.com"
            : $"{location}-aiplatform.googleapis.com";

        return $"https://{host}/v1/projects/{ProjectId}/locations/{location}/publishers/google/models/{model}:generateContent";
    }

    private static ImageLocalizationExtractionDto DeserializeExtraction(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ImageLocalizationExtractionDto();
        }

        try
        {
            return JsonConvert.DeserializeObject<ImageLocalizationExtractionDto>(content)
                   ?? throw new JsonSerializationException("The response was empty.");
        }
        catch (JsonException exception)
        {
            throw new PluginApplicationException(
                $"Could not parse localizable image content returned by Gemini: {exception.Message}",
                exception);
        }
    }

    private static string ExtractText(GeminiGenerateContentResponse response)
        => string.Concat(response.Candidates?
            .SelectMany(x => x.Content?.Parts ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text) ?? []);

    private static UsageDto ExtractUsage(GeminiGenerateContentResponse response)
        => response.UsageMetadata is null
            ? new UsageDto()
            : new UsageDto(
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount,
                response.UsageMetadata.TotalTokenCount);

    private static string BuildLocalizationPrompt(
        string replacementsJson,
        string? sourceLanguage,
        string? targetLanguage,
        string? additionalInstructions)
    {
        var languageContext = string.IsNullOrWhiteSpace(sourceLanguage) && string.IsNullOrWhiteSpace(targetLanguage)
            ? string.Empty
            : $" The source language is '{sourceLanguage ?? "unknown"}' and the target language is '{targetLanguage ?? "unknown"}'.";

        var prompt = "Edit the supplied original image by replacing its visible source-language text according to " +
                     "the exact source-to-target mappings below. Use every target string exactly as supplied; do not " +
                     "translate, paraphrase, correct, omit, duplicate, or invent text. Match repeated strings by mapping " +
                     "order and visual reading order. Remove the original source-language text. Preserve the general " +
                     "content, layout, visual style, and aspect ratio. Adjust font size, line breaks, or spacing when " +
                     "necessary to fit the original text region. Do not add explanations, labels, or watermarks." +
                     languageContext + "\n\nText mappings:\n" + replacementsJson;

        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            prompt += "\n\nAdditional instructions:\n" + additionalInstructions.Trim();
        }

        return prompt;
    }

    private static string ExtensionForMediaType(string mediaType)
        => mediaType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png"
        };

    private static string BuildOutputName(string? requestedName, string inputName, string extension)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName)
            ? Path.GetFileNameWithoutExtension(inputName)
            : Path.GetFileNameWithoutExtension(requestedName.Trim());

        return $"{baseName}{extension}";
    }
}
