using Contracts.Insights;
using FunctionApp.Persistence;
using Microsoft.Extensions.Logging;

namespace FunctionApp.Agents;

/// <summary>
/// Read-only agent that retrieves the most recent persisted business insights
/// for a job. Mirrors <see cref="JobStatusQueryAgent"/> (single responsibility,
/// resolved by its <see cref="IAgent{TInput, TOutput}"/> interface) so the read
/// path stays consistent. Returns <c>null</c> when no insights exist yet.
/// </summary>
public sealed class InsightQueryAgent
    : IAgent<Guid, BusinessInsightsV1?>
{
    private readonly InsightStore _insightStore;
    private readonly ILogger<InsightQueryAgent> _logger;

    public InsightQueryAgent(
        InsightStore insightStore,
        ILogger<InsightQueryAgent> logger)
    {
        _insightStore = insightStore;
        _logger = logger;
    }

    public async Task<BusinessInsightsV1?> ExecuteAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying insights for JobId {JobId}",
            jobId);

        var insights = await _insightStore.GetByJobIdAsync(
            jobId,
            cancellationToken);

        if (insights is null)
        {
            _logger.LogInformation(
                "No insights available yet for JobId {JobId}",
                jobId);
        }

        return insights;
    }
}
