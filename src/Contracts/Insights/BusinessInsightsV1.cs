namespace Contracts.Insights;

/// <summary>
/// AI-generated business insights derived from structured signals.
/// </summary>
/// <example>
/// {
///   "executiveSummary": "Revenue increased by 12.5% compared to the previous period.",
///   "risks": ["Customer concentration risk detected."],
///   "opportunities": ["Upsell high-value customer segments."],
///   "recommendations": ["Diversify customer base."],
///   "generatedAt": "2026-02-12T10:16:05Z"
/// }
/// </example>
public sealed record BusinessInsightsV1(
    /// <summary>
    /// High-level executive summary derived from structured signals.
    /// </summary>
    string ExecutiveSummary,

    /// <summary>
    /// Identified risks based on business signal analysis.
    /// </summary>
    IReadOnlyList<string> KeyRisks,

    /// <summary>
    /// Identified growth or optimization opportunities.
    /// </summary>
    IReadOnlyList<string> Opportunities,

    /// <summary>
    /// Recommended business actions.
    /// </summary>
    IReadOnlyList<string> Recommendations,

    /// <summary>
    /// UTC timestamp indicating when the insights were generated.
    /// </summary>
    /// <example>2026-02-12T10:16:05Z</example>
    DateTimeOffset GeneratedAt
);

