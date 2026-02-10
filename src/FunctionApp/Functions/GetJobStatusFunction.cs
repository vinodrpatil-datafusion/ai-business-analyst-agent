using Contracts.Invocation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace FunctionApp.Functions;

public sealed class GetJobStatusFunction
{
    [Function("GetJobStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "jobs/{jobId:guid}")]
        HttpRequestData request,
        Guid jobId,
        FunctionContext context)
    {
        // TODO:
        // - Query job table
        // - Return current status

        var response = request.CreateResponse(HttpStatusCode.OK);

        var status = new JobStatusResponseV1(
            JobId: jobId,
            Status: "Pending",
            LastUpdatedAt: DateTimeOffset.UtcNow,
            InsightsAvailable: false
        );

        await response.WriteAsJsonAsync(status);
        return response;
    }
}
