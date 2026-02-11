namespace Contracts.Invocation;

/// <summary>
/// Response representing the current processing status of a job.
/// </summary>
/// <example>
/// {
///   "jobId": "c8e4c8f2-3f19-4f87-9f8b-7c1f9e0f1a11",
///   "status": "Completed",
///   "insightsAvailable": true,
///   "submittedAt": "2026-02-12T10:15:30Z",
///   "lastUpdatedAt": "2026-02-12T10:16:02Z"
/// }
/// </example>
public sealed record JobStatusResponseV1(
    /// <summary>
    /// Unique identifier of the job.
    /// </summary>
    Guid JobId,

    /// <summary>
    /// Current processing status of the job.
    /// </summary>
    string Status,

    /// <summary>
    /// Indicates whether AI-generated insights are available for retrieval.
    /// </summary>
    /// <example>true</example>
    bool InsightsAvailable,

    /// <summary>
    /// UTC timestamp when the job was submitted.
    /// </summary>
    DateTimeOffset SubmittedAt,

    /// <summary>
    /// UTC timestamp of the last status update.
    /// </summary>
    DateTimeOffset LastUpdatedAt
);


