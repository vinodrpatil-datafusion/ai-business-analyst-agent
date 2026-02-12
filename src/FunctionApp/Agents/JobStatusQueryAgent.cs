using Contracts.Invocation;
using Contracts.Jobs;

namespace FunctionApp.Agents;

/// <summary>
/// Read-only agent responsible for retrieving the current
/// status of a job and whether insights are available.
/// </summary>
public sealed class JobStatusQueryAgent
    : IAgent<Guid, JobStatusResponseV1>
{
    public Task<JobStatusResponseV1> ExecuteAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // - Query Jobs table
        // - Check if insights exist
        // - Map to JobStatusResponseV1

        var response = new JobStatusResponseV1(
            JobId: jobId,
            Status: JobStatuses.Pending,
            LastUpdatedAt: DateTimeOffset.UtcNow,
            SubmittedAt: DateTimeOffset.UtcNow,
            InsightsAvailable: false
        );

        return Task.FromResult(response);
    }
}