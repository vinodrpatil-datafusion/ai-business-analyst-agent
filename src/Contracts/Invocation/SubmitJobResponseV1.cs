namespace Contracts.Invocation;

public sealed record SubmitJobResponseV1(
    Guid JobId,
    DateTimeOffset SubmittedAt
);
