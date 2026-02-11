using Contracts.Invocation;
using FunctionApp.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;

namespace FunctionApp.Functions;

public sealed class GetJobStatusFunction
{
    private readonly IAgent<Guid, JobStatusResponseV1> _statusAgent;

    public GetJobStatusFunction(
        IAgent<Guid, JobStatusResponseV1> statusAgent)
    {
        _statusAgent = statusAgent;
    }

    [Function("GetJobStatus")]
    [OpenApiOperation(
    operationId: "GetJobStatus",
    tags: new[] { "Jobs" },
    Summary = "Get job processing status",
    Description = "Returns the current status and timestamps for a submitted job."
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
    bodyType: typeof(JobStatusResponseV1),
    Summary = "Job status retrieved successfully"
    )]
    [OpenApiResponseWithoutBody(
    statusCode: HttpStatusCode.NotFound,
    Summary = "Job not found"
    )]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{jobId:guid}")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        var status = await _statusAgent.ExecuteAsync(
            jobId,
            context.CancellationToken
        );

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(status);
        return response;
    }
}
