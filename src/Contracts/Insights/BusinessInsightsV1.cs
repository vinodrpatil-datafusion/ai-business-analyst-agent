namespace Contracts.Insights;

public sealed record BusinessInsightsV1(
    string ExecutiveSummary,
    IReadOnlyList<string> KeyRisks,
    IReadOnlyList<string> Opportunities,
    IReadOnlyList<string> RecommendedActions,
    DateTimeOffset GeneratedAt
);

