namespace Contracts.Signals;

/// <summary>
/// Deterministic signals extracted from structured business data.
/// This DTO is versioned and immutable.
/// Raw data is never passed directly to the LLM.
/// </summary>
public sealed record BusinessSignalsV1(
    int RecordCount,
    DateTimeOffset GeneratedAt,
    IReadOnlyDictionary<string, decimal> NumericAverages,
    IReadOnlyDictionary<string, decimal> NumericTotals,
    IReadOnlyDictionary<string, int> CategoryCounts,
    IReadOnlyDictionary<string, ColumnMetadataV1> ColumnMetadata,
    IReadOnlyList<string> DetectedAnomalies
);