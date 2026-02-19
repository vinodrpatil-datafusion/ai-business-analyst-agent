using Contracts.Signals;

namespace FunctionApp.Agents;

/// <summary>
/// Compresses full BusinessSignals into an AI-safe summary.
/// </summary>
public sealed class InsightSignalSummarizer
{
    private const int TopCategoryLimit = 10;

    public InsightSignalSummaryV1 Summarize(BusinessSignalsV1 signals)
    {
        var totalRecords = signals.RecordCount;

        var categoryHighlights = signals.CategoryCounts
            .OrderByDescending(kv => kv.Value)
            .Take(TopCategoryLimit)
            .Select(kv =>
            {
                var split = kv.Key.Split(':');
                var column = split[0];
                var value = split.Length > 1 ? split[1] : "Unknown";

                var percentage = totalRecords == 0
                    ? 0
                    : Math.Round((decimal)kv.Value / totalRecords * 100, 2);

                return new CategoryHighlightV1(
                    Column: column,
                    Value: value,
                    Count: kv.Value,
                    Percentage: percentage
                );
            })
            .ToList();

        return new InsightSignalSummaryV1(
            RecordCount: totalRecords,
            TopNumericTotals: signals.NumericTotals
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToDictionary(k => k.Key, v => v.Value),

            TopNumericAverages: signals.NumericAverages
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToDictionary(k => k.Key, v => v.Value),

            CategoryHighlights: categoryHighlights,

            AnomalyCount: signals.DetectedAnomalies.Count,
            SampleAnomalies: signals.DetectedAnomalies.Take(5).ToList(),

            ColumnCount: signals.ColumnMetadata.Count,
            NumericColumnCount: signals.ColumnMetadata
                .Count(c => c.Value.ColumnType == InferredColumnType.Numeric),

            CategoricalColumnCount: signals.ColumnMetadata
                .Count(c => c.Value.ColumnType == InferredColumnType.Categorical)
                    );
    }
}
