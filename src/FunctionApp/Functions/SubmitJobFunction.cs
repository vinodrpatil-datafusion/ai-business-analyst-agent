using Contracts.Invocation;
using FunctionApp.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace FunctionApp.Functions;

public sealed class SubmitJobFunction
{
    private readonly JobStore _jobStore;

    public SubmitJobFunction(JobStore jobStore)
    {
        _jobStore = jobStore;
    }

    [Function("SubmitJob")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs")]
        HttpRequestData request,
        FunctionContext context)
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var submitRequest = JsonSerializer.Deserialize<SubmitJobRequestV1>(requestBody);

        if (submitRequest is null)
        {
            var bad = request.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid request body");
            return bad;
        }

        var jobId = Guid.NewGuid();

        await _jobStore.CreateJobAsync(
            jobId,
            submitRequest,
            context.CancellationToken
        );

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(
            new SubmitJobResponseV1(jobId, DateTimeOffset.UtcNow)
        );

        return response;
    }
}
