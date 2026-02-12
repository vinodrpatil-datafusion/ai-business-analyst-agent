using Contracts.Insights;
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

        // 1️ Check job exists
        var jobStatus = await _jobStore.GetStatusAsync(jobId, ct);

        if (jobStatus.Status == "NotFound")
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Job not found.");
            return notFound;
        }

        // 2️ Idempotency: reject if already completed
        if (jobStatus.Status == "Completed")
        {
            var conflict = request.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Job already completed.");
            return conflict;
        }

        // 3️ Concurrency-safe atomic transition Pending → Processing
        var locked = await _jobStore.TryMarkProcessingAsync(jobId, ct);

        if (!locked)
        {
            var conflict = request.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Job is already being processed.");
            return conflict;
        }

        try
        {
            // 4️ Retrieve BlobPath
            var blobPath = await _jobStore.GetBlobPathAsync(jobId, ct);

            if (string.IsNullOrWhiteSpace(blobPath))
                throw new InvalidOperationException("BlobPath not found for job.");

            // 5️ Deterministic signal extraction
            var signals = await _signalAgent.ExecuteAsync(blobPath, ct);
            await _signalStore.SaveAsync(jobId, signals, ct);

            // 6️ AI reasoning (agent persists insights internally)
            await _insightAgent.ExecuteAsync((jobId, signals), ct);

            // 7️ Mark Completed
            await _jobStore.UpdateStatusAsync(jobId, "Completed", ct);

            var accepted = request.CreateResponse(HttpStatusCode.Accepted);
            await accepted.WriteStringAsync("Job processed successfully.");
            return accepted;
        }
        catch
        {
            await _jobStore.UpdateStatusAsync(jobId, "Failed", ct);
            throw;
        }
    }
}
