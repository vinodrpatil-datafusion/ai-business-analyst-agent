namespace Contracts.Signals;

/// <summary>
/// Deterministically extracted business signals derived from structured input data.
/// These signals are used as input to AI-based reasoning.
/// </summary>
/// <example>
/// {
///   "recordCount": 120,
///   "numericAverages": {
///     "orderValue": 250.75
///   },
///   "numericTotals": {
///     "revenue": 30090.00
///   },
///   "categoryCounts": {
///     "region_US": 60,
///     "region_EU": 40
///   },
///   "generatedAt": "2026-02-12T10:15:45Z"
/// }
/// </example>
public sealed record BusinessSignalsV1(
    /// <summary>
    /// Total number of records processed.
    /// </summary>
    int RecordCount,

    /// <summary>
    /// Average values computed for numeric columns.
    /// </summary>
    IReadOnlyDictionary<string, decimal> NumericAverages,

    /// <summary>
    /// Total values computed for numeric columns.
    /// </summary>
    IReadOnlyDictionary<string, decimal> NumericTotals,

    /// <summary>
    /// Counts of categorical values grouped by column.
    /// </summary>
    IReadOnlyDictionary<string, int> CategoryCounts,

    /// <summary>
    /// UTC timestamp indicating when signals were generated.
    /// </summary>
    DateTimeOffset GeneratedAt
);

