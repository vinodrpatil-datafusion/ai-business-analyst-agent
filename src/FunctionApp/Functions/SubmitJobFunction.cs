using Contracts.Invocation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace FunctionApp.Functions;

public sealed class SubmitJobFunction
{
    [Function("SubmitJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData request,
        FunctionContext context)
    {
        // TODO:
        // - Validate request
        // - Persist job metadata
        // - Upload file to Blob Storage
        // - Enqueue processing

        var response = request.CreateResponse(HttpStatusCode.Accepted);

        var result = new SubmitJobResponseV1(
            JobId: Guid.NewGuid(),
            SubmittedAt: DateTimeOffset.UtcNow
        );

        await response.WriteAsJsonAsync(result);
        return response;
    }
}
