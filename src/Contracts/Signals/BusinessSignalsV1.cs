namespace Contracts.Signals;

public sealed record BusinessSignalsV1(
    int RecordCount,
    IReadOnlyDictionary<string, decimal> NumericAverages,
    IReadOnlyDictionary<string, decimal> NumericTotals,
    IReadOnlyDictionary<string, int> CategoryCounts
);
