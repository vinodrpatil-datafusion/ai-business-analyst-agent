using Contracts.Insights;
using Contracts.Signals;

namespace FunctionApp.Agents;

public sealed class InsightReasoningAgent
    : IAgent<BusinessSignalsV1, BusinessInsightsV1>
{
    public Task<BusinessInsightsV1> ExecuteAsync(
        BusinessSignalsV1 signals,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // - Build structured prompt
        // - Call Azure OpenAI
        // - Parse response safely

        var insights = new BusinessInsightsV1(
            ExecutiveSummary: "Insights generation pending.",
            KeyRisks: Array.Empty<string>(),
            Opportunities: Array.Empty<string>(),
            RecommendedActions: Array.Empty<string>(),
            GeneratedAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult(insights);
    }
}
