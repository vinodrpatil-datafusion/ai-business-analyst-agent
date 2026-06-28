using Contracts.Invocation;
using FunctionApp.Parsing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;

namespace FunctionApp.Functions;

/// <summary>
/// Mints a short-lived, write-scoped SAS URL so a client (e.g. the browser)
/// can upload a CSV/Excel file directly to Blob Storage without any keys.
/// The flow is: POST /uploads -> PUT file to the returned uploadUrl ->
/// POST /jobs { blobPath } to register the job.
/// </summary>
public sealed class CreateUploadFunction
{
    private readonly BlobUploadUrlIssuer _issuer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Mirrors the formats FileParserFactory can actually parse.
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csv", ".xlsx", ".xls" };

    public CreateUploadFunction(BlobUploadUrlIssuer issuer)
    {
        _issuer = issuer;
    }

    [Function("CreateUpload")]
    [OpenApiOperation(
        operationId: "CreateUpload",
        tags: new[] { "Uploads" },
        Summary = "Mint a direct-to-blob upload URL",
        Description = "Returns a short-lived, write-scoped SAS URL for uploading a CSV/Excel file directly to Blob Storage, plus the blob path to register a job with."
    )]
    [OpenApiRequestBody(
        contentType: "application/json",
        bodyType: typeof(CreateUploadRequestV1),
        Required = true,
        Description = "File metadata (used only to validate the extension)."
    )]
    [OpenApiResponseWithBody(
        statusCode: HttpStatusCode.OK,
        contentType: "application/json",
        bodyType: typeof(CreateUploadResponseV1),
        Summary = "Upload URL minted",
        Description = "Returns blobPath, uploadUrl (with SAS) and expiresAt."
    )]
    [OpenApiResponseWithoutBody(
        statusCode: HttpStatusCode.BadRequest,
        Summary = "Missing file name or unsupported file type"
    )]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "uploads")]
        HttpRequestData request,
        FunctionContext context)
    {
        var ct = context.CancellationToken;

        CreateUploadRequestV1? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateUploadRequestV1>(
                request.Body, JsonOptions, ct);
        }
        catch (JsonException)
        {
            return await BadRequest(request, "Invalid request body.");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.FileName))
            return await BadRequest(request, "fileName is required.");

        var ext = Path.GetExtension(body.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return await BadRequest(request,
                "Unsupported file type. Allowed: .csv, .xlsx, .xls.");

        // Server-generated, non-guessable blob name. The client's file name is
        // never used as the path (avoids traversal / overwrite).
        var blobName = $"{Guid.NewGuid():N}{ext}";

        var (blobPath, uploadUrl, expiresAt) =
            await _issuer.CreateUploadUrlAsync(blobName, ct);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(
            new CreateUploadResponseV1(
                BlobPath: blobPath,
                UploadUrl: uploadUrl.ToString(),
                ExpiresAt: expiresAt),
            ct);
        return response;
    }

    private static async Task<HttpResponseData> BadRequest(
        HttpRequestData request, string message)
    {
        var bad = request.CreateResponse(HttpStatusCode.BadRequest);
        await bad.WriteStringAsync(message);
        return bad;
    }
}
