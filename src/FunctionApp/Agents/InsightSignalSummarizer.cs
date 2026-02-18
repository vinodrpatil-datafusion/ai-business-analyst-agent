using Contracts.Signals;

namespace FunctionApp.Agents;

/// <summary>
/// Compresses full BusinessSignals into an AI-safe summary.
/// </summary>
public sealed class InsightSignalSummarizer
{
    public InsightSignalSummaryV1 Summarize(BusinessSignalsV1 signals)
    {
        var compression = GetCompressionLevel(signals.RecordCount);

        var topTotals = signals.NumericTotals
            .OrderByDescending(x => x.Value)
            .Take(compression.NumericLimit)
            .ToDictionary(x => x.Key, x => x.Value);

        var topAverages = signals.NumericAverages
            .OrderByDescending(x => x.Value)
            .Take(compression.NumericLimit)
            .ToDictionary(x => x.Key, x => x.Value);

        var categoryHighlights = signals.CategoryCounts
            .OrderByDescending(x => x.Value)
            .Take(compression.CategoryLimit)
            .Select(x => $"{x.Key} = {x.Value}")
            .ToList();

        var anomalies = signals.DetectedAnomalies
            .Take(compression.AnomalyLimit)
            .ToList();

        var numericCount = signals.ColumnMetadata
            .Count(c => c.Value.InferredType == "Numeric");

        var categoricalCount = signals.ColumnMetadata
            .Count(c => c.Value.InferredType == "Categorical");

        return new InsightSignalSummaryV1(
            RecordCount: signals.RecordCount,
            TopNumericTotals: topTotals,
            TopNumericAverages: topAverages,
            CategoryHighlights: categoryHighlights,
            AnomalyCount: signals.DetectedAnomalies.Count,
            SampleAnomalies: anomalies,
            ColumnCount: signals.ColumnMetadata.Count,
            NumericColumnCount: numericCount,
            CategoricalColumnCount: categoricalCount
        );
    }

    private static (int NumericLimit, int CategoryLimit, int AnomalyLimit)
        GetCompressionLevel(int recordCount)
    {
        if (recordCount > 100000)
            return (3, 5, 3);

        if (recordCount > 10000)
            return (5, 10, 5);

        return (10, 20, 5);
    }
}
