using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace FunctionApp.Parsing;

/// <summary>
/// Mints short-lived, write-scoped upload URLs for direct browser-to-Blob
/// uploads, using a <b>user-delegation SAS</b>.
///
/// The SAS is signed with a user-delegation key obtained from Entra via the
/// app's <see cref="TokenCredential"/> (managed identity in Azure, developer
/// sign-in locally) — NOT the storage account key. This keeps the system
/// secretless: no account key is held, configured, or distributed.
///
/// Least privilege: the issued SAS grants Create + Write on a single named
/// blob (not the container), over HTTPS only, for a short validity window.
/// A leaked token can therefore only write the one blob it was minted for,
/// and only briefly.
///
/// NOTE: a user-delegation SAS cannot grant more than the issuing identity's
/// own RBAC. Minting a write SAS therefore requires the identity to hold
/// write on the uploads container (Storage Blob Data Contributor, scoped to
/// the container) plus Storage Blob Delegator (to obtain the delegation key).
/// </summary>
public sealed class BlobUploadUrlIssuer
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly TimeSpan _validity;

    // Small backdating to tolerate clock skew between this host and storage.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public BlobUploadUrlIssuer(
        Uri serviceUri,
        TokenCredential credential,
        string containerName,
        TimeSpan validity)
    {
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("containerName cannot be empty.", nameof(containerName));

        _blobServiceClient = new BlobServiceClient(serviceUri, credential);
        _containerName = containerName;
        _validity = validity;
    }

    /// <summary>
    /// Creates a write-scoped SAS URL for the given blob name.
    /// </summary>
    public async Task<(string BlobPath, Uri UploadUrl, DateTimeOffset ExpiresAt)>
        CreateUploadUrlAsync(string blobName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("blobName cannot be empty.", nameof(blobName));

        var startsOn = DateTimeOffset.UtcNow - ClockSkew;
        var expiresOn = DateTimeOffset.UtcNow + _validity;

        // Entra-signed delegation key (no account key involved).
        var userDelegationKey = await _blobServiceClient
            .GetUserDelegationKeyAsync(startsOn, expiresOn, ct)
            .ConfigureAwait(false);

        var blobClient = _blobServiceClient
            .GetBlobContainerClient(_containerName)
            .GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b", // single blob
            StartsOn = startsOn,
            ExpiresOn = expiresOn,
            Protocol = SasProtocol.Https
        };

        // Just enough to upload once — no read, list, or delete.
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        var sasToken = sasBuilder
            .ToSasQueryParameters(userDelegationKey.Value, _blobServiceClient.AccountName)
            .ToString();

        var uploadUrl = new UriBuilder(blobClient.Uri) { Query = sasToken }.Uri;

        return (blobName, uploadUrl, expiresOn);
    }
}
