using Contracts.Invocation;
using FunctionApp.Agents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
