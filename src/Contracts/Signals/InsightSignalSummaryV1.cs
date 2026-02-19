namespace Contracts.Signals;

/// <summary>
/// AI-safe summarized representation of deterministic business signals.
/// Designed to prevent token explosion in LLM prompts.
/// </summary>
public sealed record InsightSignalSummaryV1(
    int RecordCount,

    IReadOnlyDictionary<string, decimal> TopNumericTotals,
    IReadOnlyDictionary<string, decimal> TopNumericAverages,

    IReadOnlyList<CategoryHighlightV1> CategoryHighlights,

    int AnomalyCount,
    IReadOnlyList<string> SampleAnomalies,

    int ColumnCount,
    int NumericColumnCount,
    int CategoricalColumnCount
);

public sealed record CategoryHighlightV1(
    string Column,
    string Value,
    int Count,
    decimal Percentage
);
