namespace Contracts.Invocation;

/// <summary>
/// Request to submit a new business analysis job.
/// The BlobName must reference an existing file in Azure Blob Storage.
/// </summary>
/// <example>
/// {
///   "blobName": "sales-report-q1-2026.csv"
/// }
/// </example>
public sealed record SubmitJobRequestV1(
    /// <summary>
    /// Name of the blob file stored in Azure Blob Storage.
    /// </summary>
    /// <example>sales-report-q1-2026.csv</example>
    string BlobPath
);

