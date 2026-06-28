namespace Contracts.Invocation;

/// <summary>
/// Response containing a short-lived, write-scoped SAS URL for a direct
/// browser-to-Blob upload, plus the blob path to register the subsequent job.
/// </summary>
/// <example>
/// {
///   "blobPath": "9f1c2e7a4b8d4e10a2c3d4e5f6a7b8c9.csv",
///   "uploadUrl": "https://acct.blob.core.windows.net/uploads/9f1c...csv?sv=...&sig=...",
///   "expiresAt": "2026-06-28T10:30:00Z"
/// }
/// </example>
public sealed record CreateUploadResponseV1(
    /// <summary>
    /// Server-generated blob name within the uploads container. Pass this as
    /// <c>BlobPath</c> to <c>POST /jobs</c> after the upload completes.
    /// </summary>
    string BlobPath,

    /// <summary>
    /// Full HTTPS URL (including the SAS token) the client PUTs the file bytes
    /// to. Grants create+write on this single blob only, and expires shortly.
    /// </summary>
    string UploadUrl,

    /// <summary>
    /// UTC instant after which <see cref="UploadUrl"/> is no longer valid.
    /// </summary>
    DateTimeOffset ExpiresAt
);
