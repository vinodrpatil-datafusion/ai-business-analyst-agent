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
    private readonly SignalStore _signalStore;
    private readonly InsightStore _insightStore;
    private readonly JobStore _jobStore;

    public ProcessJobFunction(
        IAgent<string, BusinessSignalsV1> signalAgent,
        IAgent<BusinessSignalsV1, BusinessInsightsV1> insightAgent,
        SignalStore signalStore,
        InsightStore insightStore,
        JobStore jobStore)
    {
        _signalAgent = signalAgent;
        _insightAgent = insightAgent;
        _signalStore = signalStore;
        _insightStore = insightStore;
        _jobStore = jobStore;
    }

    [Function("ProcessJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId:guid}/process")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        // 1. Mark job as Processing
        await _jobStore.UpdateStatusAsync(
            jobId,
            "Processing",
            context.CancellationToken
        );

        // NOTE:
        // For now, the input to SignalExtractionAgent is a placeholder.
        // Later this will be blob path or file reference.
        var inputReference = $"job:{jobId}";

        // 2. Deterministic signal extraction
        var signals = await _signalAgent.ExecuteAsync(
            inputReference,
            context.CancellationToken
        );

        await _signalStore.SaveAsync(
            jobId,
            signals,
            context.CancellationToken
        );

        // 3. AI reasoning
        var insights = await _insightAgent.ExecuteAsync(
            signals,
            context.CancellationToken
        );

        await _insightStore.SaveAsync(
            jobId,
            insights,
            context.CancellationToken
        );

        // 4. Mark job as Completed
        await _jobStore.UpdateStatusAsync(
            jobId,
            "Completed",
            context.CancellationToken
        );

        // 5. Immediate acknowledgement
        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(
            new { JobId = jobId, Status = "Completed" }
        );

        return response;
    }
}
