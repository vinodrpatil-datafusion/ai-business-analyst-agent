namespace Contracts.Invocation;

/// <summary>
/// Request to mint a short-lived, direct-to-blob upload URL.
/// The file is not uploaded through the API; the client uploads the bytes
/// directly to Blob Storage using the returned SAS URL, then registers a job
/// via <c>POST /jobs</c> with the returned <c>BlobPath</c>.
/// </summary>
/// <example>
/// {
///   "fileName": "sales-report-q1-2026.csv"
/// }
/// </example>
public sealed record CreateUploadRequestV1(
    /// <summary>
    /// Original file name. Used only to validate and derive the file
    /// extension (.csv / .xlsx / .xls). It is NOT used as the blob path —
    /// the server generates a unique, non-guessable blob name.
    /// </summary>
    /// <example>sales-report-q1-2026.csv</example>
    string FileName
);
