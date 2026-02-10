using Contracts.Invocation;
using Contracts.Insights;
using Contracts.Signals;
using FunctionApp.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace FunctionApp.Functions;

public sealed class ProcessJobFunction
{
    private readonly IAgent<string, BusinessSignalsV1> _signalAgent;
    private readonly IAgent<BusinessSignalsV1, BusinessInsightsV1> _insightAgent;

    public ProcessJobFunction(
        IAgent<string, BusinessSignalsV1> signalAgent,
        IAgent<BusinessSignalsV1, BusinessInsightsV1> insightAgent)
    {
        _signalAgent = signalAgent;
        _insightAgent = insightAgent;
    }

    [Function("ProcessJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobId:guid}/process")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        // TODO (non-blocking):
        // - Validate jobId
        // - Mark job as Processing
        // - Kick off background execution (agent pipeline)
        // - Persist results asynchronously

        var response = request.CreateResponse(HttpStatusCode.Accepted);

        var status = new JobStatusResponseV1(
            JobId: jobId,
            Status: "Processing",
            LastUpdatedAt: DateTimeOffset.UtcNow,
            InsightsAvailable: false
        );

        await response.WriteAsJsonAsync(status);
        return response;
    }
}
