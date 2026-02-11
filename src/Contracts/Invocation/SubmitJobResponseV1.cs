namespace Contracts.Invocation;

/// <summary>
/// Response returned after successfully registering a job.
/// </summary>
/// <example>
/// {
///   "jobId": "c8e4c8f2-3f19-4f87-9f8b-7c1f9e0f1a11",
///   "submittedAt": "2026-02-12T10:15:30Z"
/// }
/// </example>
public sealed record SubmitJobResponseV1(
    /// <summary>
    /// Unique identifier of the submitted job.
    /// </summary>
    /// <example>c8e4c8f2-3f19-4f87-9f8b-7c1f9e0f1a11</example>
    Guid JobId,

    /// <summary>
    /// UTC timestamp indicating when the job was submitted.
    /// </summary>
    /// <example>2026-02-12T10:15:30Z</example>
    DateTimeOffset SubmittedAt
);