using Apps.GoogleVertexAI.Constants;
using Apps.GoogleVertexAI.Factories;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Google.Cloud.AIPlatform.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Apps.GoogleVertexAI.Polling
{
    [PollingEventList]
    public class BatchPolling : VertexAiInvocable
    {
        public BatchPolling(InvocationContext invocationContext) : base(invocationContext) { }

        [PollingEvent("On batch finished", "Triggered when a Vertex AI batch job reaches a terminal state (Succeeded/Failed/Cancelled).")]
        public async Task<PollingEventResponse<BatchMemory, BatchStatusResponse>> OnBatchFinished(PollingEventRequest<BatchMemory> request,
            [PollingEventParameter] BatchIdentifier identifier)
        {
            if (request.Memory is null)
            {
                return new()
                {
                    FlyBird = false,
                    Memory = new BatchMemory
                    {
                        LastPollingTime = DateTime.UtcNow,
                        Triggered = false
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(identifier.JobName))
                throw new PluginMisconfigurationException("Job name is required.");

            var region = TryGetLocationFromJobName(identifier.JobName, out var loc)
                ? loc
                : throw new PluginMisconfigurationException(
                    "Invalid job name: cannot extract location. Expected 'projects/{p}/locations/{loc}/batchPredictionJobs/{id}'.");

            var jobClient = ClientFactory.CreateJobService(InvocationContext.AuthenticationCredentialsProviders, region);
            var job = jobClient.GetBatchPredictionJob(identifier.JobName);

            var result = new BatchStatusResponse
            {
                State = job.State.ToString(),
                SuccessfulCount = job.CompletionStats?.SuccessfulCount ?? 0,
                FailedCount = job.CompletionStats?.FailedCount ?? 0,
                OutputUriPrefix = job.OutputConfig?.GcsDestination?.OutputUriPrefix,
                ErrorCode = job.Error?.Code.ToString(),
                ErrorMessage = job.Error?.Message,
                PartialFailures = job.PartialFailures?.Select(pf => $"{pf.Code}: {pf.Message}")?.ToList()
            };

            var isTerminal =
                job.State == JobState.Succeeded ||
                job.State == JobState.Failed ||
                job.State == JobState.Cancelled;

            var triggeredNow = isTerminal && !request.Memory.Triggered;

            return new()
            {
                FlyBird = triggeredNow,
                Result = isTerminal ? result : null,
                Memory = new BatchMemory
                {
                    LastPollingTime = DateTime.UtcNow,
                    Triggered = request.Memory.Triggered || isTerminal
                }
            };
        }

        private static bool TryGetLocationFromJobName(string jobName, out string location)
        {
            location = string.Empty;

            if (BatchPredictionJobName.TryParse(jobName, out var rn))
            {
                location = rn.LocationId;
                return !string.IsNullOrWhiteSpace(location);
            }

            var m = Regex.Match(jobName,
                @"^projects/[^/]+/locations/(?<loc>[^/]+)/batchPredictionJobs/[^/]+$",
                RegexOptions.IgnoreCase);

            if (m.Success)
            {
                location = m.Groups["loc"].Value;
                return true;
            }

            return false;
        }
    }
}
