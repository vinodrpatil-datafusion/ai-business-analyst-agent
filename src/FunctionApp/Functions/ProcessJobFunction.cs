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
        Description = "Triggers deterministic signal extraction and AI-based insight generation for a job."
    )]
    [OpenApiParameter(
        name: "jobId",
        In = ParameterLocation.Path,
        Required = true,
        Type = typeof(Guid),
        Summary = "Unique identifier of the job to process"
    )]
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.Accepted,
        Summary = "Job processing started"
    )]
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.NotFound,
        Summary = "Job not found"
    )]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId:guid}/process")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        var ct = context.CancellationToken;

        // 1️ Check if job exists first
        var jobStatus = await _jobStore.GetStatusAsync(jobId, ct);

        if (jobStatus.Status == "NotFound")
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Job not found");
            return notFound;
        }

        try
        {
            // 2️ Mark job as Processing
            await _jobStore.UpdateStatusAsync(jobId, "Processing", ct);

            // 3️ Get BlobPath
            var blobPath = await _jobStore.GetBlobPathAsync(jobId, ct);

            if (string.IsNullOrWhiteSpace(blobPath))
                throw new InvalidOperationException("BlobPath not found for job");

            // 4️ Deterministic signal extraction
            var signals = await _signalAgent.ExecuteAsync(blobPath, ct);
            await _signalStore.SaveAsync(jobId, signals, ct);

            // 5️ AI reasoning (agent now persists insights internally)
            await _insightAgent.ExecuteAsync((jobId, signals), ct);

            // 6️ Mark job as Completed
            await _jobStore.UpdateStatusAsync(jobId, "Completed", ct);

            var accepted = request.CreateResponse(HttpStatusCode.Accepted);
            await accepted.WriteStringAsync("Job processed successfully.");
            return accepted;
        }
        catch (Exception)
        {
            await _jobStore.UpdateStatusAsync(jobId, "Failed", ct);
            throw;
        }
    }
}