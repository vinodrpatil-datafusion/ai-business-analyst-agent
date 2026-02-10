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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        // The UI sends intent + blob reference, not raw data
        var submitRequest = await JsonSerializer.DeserializeAsync<SubmitJobRequestV1>(
            request.Body,
            JsonOptions,
            context.CancellationToken);

        if (submitRequest is null ||
            string.IsNullOrWhiteSpace(submitRequest.BlobPath))
        {
            var bad = request.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("BlobPath is required");
            return bad;
        }

        var jobId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await _jobStore.CreateJobAsync(
            jobId,
            submitRequest,
            context.CancellationToken);

        var response = request.CreateResponse(HttpStatusCode.Accepted);

        // REST-friendly: tell the caller where to check status
        response.Headers.Add("Location", $"/api/jobs/{jobId}");

        await response.WriteAsJsonAsync(
            new SubmitJobResponseV1(
                JobId: jobId,
                SubmittedAt: now));

        return response;
    }
}
