using Contracts.Insights;
using FunctionApp.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;

namespace FunctionApp.Functions;

/// <summary>
/// Returns the persisted AI insights for a job. The content already exists in
/// the BusinessInsights table once processing has completed; this endpoint is
/// the read path the UI's Insights screen renders from. Status/readiness is
/// polled separately via GET /jobs/{jobId} (insightsAvailable).
/// </summary>
public sealed class GetInsightsFunction
{
    private readonly IAgent<Guid, BusinessInsightsV1?> _insightAgent;

    public GetInsightsFunction(
        IAgent<Guid, BusinessInsightsV1?> insightAgent)
    {
        _insightAgent = insightAgent;
    }

    [Function("GetInsights")]
    [OpenApiOperation(
        operationId: "GetInsights",
        tags: new[] { "Jobs" },
        Summary = "Get generated insights for a job",
        Description = "Returns the persisted BusinessInsightsV1 (latest attempt) for a completed job."
    )]
    [OpenApiParameter(
        name: "jobId",
        In = ParameterLocation.Path,
        Required = true,
        Type = typeof(Guid),
        Summary = "Unique identifier of the job"
    )]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK,
        contentType: "application/json",
        bodyType: typeof(BusinessInsightsV1),
        Summary = "Insights retrieved successfully"
    )]
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.NotFound,
        Summary = "No insights available for this job (unknown job or not yet processed)"
    )]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{jobId:guid}/insights")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        var insights = await _insightAgent.ExecuteAsync(
            jobId,
            context.CancellationToken);

        if (insights is null)
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("No insights available for this job.");
            return notFound;
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(insights);
        return response;
    }
}
