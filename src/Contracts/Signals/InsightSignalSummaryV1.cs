namespace Contracts.Signals;

/// <summary>
/// AI-safe summarized representation of deterministic business signals.
/// Designed to prevent token explosion in LLM prompts.
/// </summary>
public sealed record InsightSignalSummaryV1(
    int RecordCount,
    IReadOnlyDictionary<string, decimal> TopNumericTotals,
    IReadOnlyDictionary<string, decimal> TopNumericAverages,
    IReadOnlyList<string> CategoryHighlights,
    int AnomalyCount,
    IReadOnlyList<string> SampleAnomalies,
    int ColumnCount,
    int NumericColumnCount,
    int CategoricalColumnCount
);
