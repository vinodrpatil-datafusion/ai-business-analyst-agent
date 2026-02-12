using Contracts.Invocation;
using Contracts.Jobs;
using FunctionApp.Persistence;
using Microsoft.Extensions.Logging;

namespace FunctionApp.Agents;

/// <summary>
/// Read-only agent responsible for retrieving the current
/// status of a job and whether insights are available.
/// </summary>
public sealed class JobStatusQueryAgent
    : IAgent<Guid, JobStatusResponseV1?>
{
    private readonly JobStore _jobStore;
    private readonly ILogger<JobStatusQueryAgent> _logger;

    public JobStatusQueryAgent(
        JobStore jobStore,
        ILogger<JobStatusQueryAgent> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the current persisted job status from storage.
    /// Returns null if the job does not exist.
    /// </summary>
    public async Task<JobStatusResponseV1?> ExecuteAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying job status for JobId {JobId}",
            jobId);

        var result = await _jobStore.GetStatusAsync(
            jobId,
            cancellationToken);

        if (result is null)
        {
            _logger.LogWarning(
                "JobId {JobId} not found in database",
                jobId);

            return null;
        }

        _logger.LogInformation(
            "JobId {JobId} status: {Status}, InsightsAvailable: {Insights}",
            jobId,
            result.Status,
            result.InsightsAvailable);

        return result;
    }
}
