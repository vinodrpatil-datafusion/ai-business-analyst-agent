using Contracts.Insights;
using Contracts.Jobs;
using Contracts.Signals;
using FunctionApp.Agents;
using FunctionApp.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;

namespace FunctionApp.Functions;

public sealed class ProcessJobFunction
{
    private readonly IAgent<string, BusinessSignalsV1> _signalAgent;
    private readonly IAgent<(Guid JobId, BusinessSignalsV1 Signals), BusinessInsightsV1> _insightAgent;
    private readonly JobStore _jobStore;
    private readonly SignalStore _signalStore;

    public ProcessJobFunction(
        IAgent<string, BusinessSignalsV1> signalAgent,
        IAgent<(Guid JobId, BusinessSignalsV1 Signals), BusinessInsightsV1> insightAgent,
        JobStore jobStore,
        SignalStore signalStore)
    {
        _signalAgent = signalAgent;
        _insightAgent = insightAgent;
        _jobStore = jobStore;
        _signalStore = signalStore;
    }

    [Function("ProcessJob")]
    [OpenApiOperation(
        operationId: "ProcessJob",
        tags: new[] { "Jobs" },
        Summary = "Process a submitted job",
        Description = "Triggers deterministic signal extraction and AI-based insight generation."
    )]
    [OpenApiParameter(
        name: "jobId",
        In = ParameterLocation.Path,
        Required = true,
        Type = typeof(Guid),
        Summary = "Unique identifier of the job"
    )]
    [OpenApiResponseWithoutBody(HttpStatusCode.Accepted)]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound)]
    [OpenApiResponseWithoutBody(HttpStatusCode.Conflict)]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId:guid}/process")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        var ct = context.CancellationToken;

        // ------------------------------------------------------------
        // 1. Validate job existence
        // ------------------------------------------------------------
        var jobStatus = await _jobStore.GetStatusAsync(jobId, ct);

        if (jobStatus is null)
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Job not found.");
            return notFound;
        }

        // ------------------------------------------------------------
        // 2. Idempotency guard (prevent reprocessing completed jobs)
        // ------------------------------------------------------------
        if (jobStatus.Status == JobStatuses.Completed)
        {
            var conflict = request.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Job already completed.");
            return conflict;
        }

        // ------------------------------------------------------------
        // 3. Concurrency-safe transition: Pending → Processing
        // ------------------------------------------------------------
        var locked = await _jobStore.TryMarkProcessingAsync(jobId, ct);

        if (!locked)
        {
            var conflict = request.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Job already being processed.");
            return conflict;
        }

        // ------------------------------------------------------------
        // 4. Apply execution timeout safeguard (90 seconds)
        // ------------------------------------------------------------
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        //timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            // ------------------------------------------------------------
            // 5. Resolve BlobPath
            // ------------------------------------------------------------
            var blobPath =
                await _jobStore.GetBlobPathAsync(jobId, timeoutCts.Token);

            if (string.IsNullOrWhiteSpace(blobPath))
                throw new InvalidOperationException("BlobPath missing.");

            // ------------------------------------------------------------
            // 6. Deterministic signal extraction
            // ------------------------------------------------------------
            var signals =
                await _signalAgent.ExecuteAsync(blobPath, timeoutCts.Token);

            await _signalStore.SaveAsync(jobId, signals, timeoutCts.Token);

            // ------------------------------------------------------------
            // 7. AI reasoning
            // ------------------------------------------------------------
            await _insightAgent.ExecuteAsync(
                (jobId, signals),
                timeoutCts.Token);

            // ------------------------------------------------------------
            // 8. Mark job as Completed
            // ------------------------------------------------------------
            await _jobStore.MarkCompletedAsync(jobId, timeoutCts.Token);

            var accepted = request.CreateResponse(HttpStatusCode.Accepted);
            await accepted.WriteStringAsync("Job processed successfully.");
            return accepted;
        }
        catch
        {
            // ------------------------------------------------------------
            // 9. Failure handling
            // ------------------------------------------------------------
            await _jobStore.MarkFailedAsync(jobId, ct);
            throw;
        }
    }
}