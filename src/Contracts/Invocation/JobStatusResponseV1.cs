namespace Contracts.Invocation;

public sealed record JobStatusResponseV1(
    Guid JobId,
    string Status,              // Pending | Processing | Completed | Failed
    DateTimeOffset LastUpdatedAt,
    bool InsightsAvailable
);
