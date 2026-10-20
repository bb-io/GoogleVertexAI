using Apps.GoogleVertexAI.Ai;
using Apps.GoogleVertexAI.Clients;
using Apps.GoogleVertexAI.Invocables;
using Apps.GoogleVertexAI.Models.Requests;
using Apps.GoogleVertexAI.Models.Response;
using Apps.GoogleVertexAI.Polling.Model;
using Blackbird.Ai.Llm.Models;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Google.Cloud.AIPlatform.V1;

namespace Apps.GoogleVertexAI.Polling
{
    [PollingEventList("Background")]
    public class BatchPolling : VertexAiInvocable
    {
        public BatchPolling(InvocationContext invocationContext) : base(invocationContext) { }

        [PollingEvent("On background job finished", "Triggered once when a Vertex AI background job reaches a terminal state (Succeeded/Partially succeeded/Failed/Cancelled/Expired).")]
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

            var result = string.IsNullOrWhiteSpace(identifier.JobName)
                ? new BatchStatusResponse { State = JobState.Succeeded.ToString() }
                : await GetStatusAsync(identifier.JobName);

            var finished = VertexBatchLlmClient.StateOf(Enum.Parse<JobState>(result.State)) != BatchJobState.Running;
            return new()
            {
                FlyBird = finished && !request.Memory.Triggered,
                Result = finished ? result : null,
                Memory = new BatchMemory
                {
                    LastPollingTime = DateTime.UtcNow,
                    Triggered = request.Memory.Triggered || finished
                }
            };
        }

        private async Task<BatchStatusResponse> GetStatusAsync(string jobName)
        {
            var job = await VertexBatchJobs.GetAsync(InvocationContext.AuthenticationCredentialsProviders, jobName);

            return new BatchStatusResponse
            {
                State = job.State.ToString(),
                SuccessfulCount = job.CompletionStats?.SuccessfulCount ?? 0,
                FailedCount = job.CompletionStats?.FailedCount ?? 0,
                OutputUriPrefix = job.OutputConfig?.GcsDestination?.OutputUriPrefix,
                ErrorCode = job.Error?.Code.ToString(),
                ErrorMessage = job.Error?.Message,
                PartialFailures = job.PartialFailures?.Select(pf => $"{pf.Code}: {pf.Message}")?.ToList()
            };
        }
    }
}
