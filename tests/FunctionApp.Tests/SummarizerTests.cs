using System.Collections.Generic;
using Contracts.Signals;
using FunctionApp.Agents;
using Xunit;

namespace FunctionApp.Tests;

public class SummarizerTests
{
    [Fact]
    public void Summarize_ReturnsExpectedTopCounts()
    {
        var signals = new BusinessSignalsV1(
            RecordCount: 3,
            GeneratedAt: System.DateTimeOffset.UtcNow,
            NumericAverages: new Dictionary<string, decimal> { { "Sales", 50m } },
            NumericTotals: new Dictionary<string, decimal> { { "Sales", 150m } },
            CategoryCounts: new Dictionary<string, int> { { "Region:North", 2 }, { "Region:South", 1 } },
            ColumnMetadata: new Dictionary<string, ColumnMetadataV1>(),
            DetectedAnomalies: new List<string>()
        );

        var summarizer = new InsightSignalSummarizer();
        var summary = summarizer.Summarize(signals);

        Assert.Equal(3, summary.RecordCount);
        Assert.Single(summary.TopNumericTotals);
        Assert.Equal(2, summary.CategoryHighlights.Count);
    }
}
