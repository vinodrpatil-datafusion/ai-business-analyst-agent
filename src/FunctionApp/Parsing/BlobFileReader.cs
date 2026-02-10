using Azure.Storage.Blobs;
using System.Text;

namespace FunctionApp.Parsing;

/// <summary>
/// Lightweight helper to read files from Azure Blob Storage.
/// Keeps file access out of agents and functions.
/// </summary>
public sealed class BlobFileReader
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobFileReader(string connectionString)
    {
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> ReadTextAsync(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobPath);

        using var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}

