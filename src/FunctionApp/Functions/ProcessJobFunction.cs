using Contracts.Insights;
using Contracts.Signals;
using FunctionApp.Agents;
using FunctionApp.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace FunctionApp.Functions;

public sealed class ProcessJobFunction
{
    private readonly IAgent<string, BusinessSignalsV1> _signalAgent;
    private readonly IAgent<BusinessSignalsV1, BusinessInsightsV1> _insightAgent;
    private readonly JobStore _jobStore;
    private readonly SignalStore _signalStore;
    private readonly InsightStore _insightStore;

    public ProcessJobFunction(
        IAgent<string, BusinessSignalsV1> signalAgent,
        IAgent<BusinessSignalsV1, BusinessInsightsV1> insightAgent,
        JobStore jobStore,
        SignalStore signalStore,
        InsightStore insightStore)
    {
        _signalAgent = signalAgent;
        _insightAgent = insightAgent;
        _jobStore = jobStore;
        _signalStore = signalStore;
        _insightStore = insightStore;
    }

    [Function("ProcessJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId:guid}/process")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        try
        {
            // 1. Mark job as Processing
            await _jobStore.UpdateStatusAsync(jobId, "Processing", context.CancellationToken);

            // 2. Fetch BlobPath
            var jobStatus = await _jobStore.GetStatusAsync(jobId, context.CancellationToken);

            if (jobStatus.Status == "NotFound")
            {
                var notFound = request.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Job not found");
                return notFound;
            }

            // NOTE:
            // BlobPath is stored in Jobs table and should be fetched
            // via a dedicated method in JobStore.
            // For now, we assume it is already available.
            // (Next refactor can add GetBlobPathAsync)

            var blobPath = await _jobStore.GetBlobPathAsync(jobId, context.CancellationToken);

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                throw new InvalidOperationException("BlobPath not found for job");
            }


            // 3. Deterministic signal extraction
            var signals = await _signalAgent.ExecuteAsync(blobPath, context.CancellationToken);
            await _signalStore.SaveAsync(jobId, signals, context.CancellationToken);

            // 4. AI reasoning
            var insights = await _insightAgent.ExecuteAsync(signals, context.CancellationToken);
            await _insightStore.SaveAsync(jobId, insights, context.CancellationToken);

            // 5. Mark job as Completed
            await _jobStore.UpdateStatusAsync(jobId, "Completed", context.CancellationToken);

            var accepted = request.CreateResponse(HttpStatusCode.Accepted);
            await accepted.WriteStringAsync("Job processed");
            return accepted;
        }
        catch (Exception)
        {
            await _jobStore.UpdateStatusAsync(jobId, "Failed", context.CancellationToken);
            throw;
        }
    }
}
