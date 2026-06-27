using Contracts.Signals;
using System.Globalization;

namespace FunctionApp.Analysis;

/// <summary>
/// Infers column data types based on sampled values.
/// Supports Numeric, DateTime, Boolean, Text, and Categorical.
/// </summary>
public static class ColumnTypeInference
{
    private const int SampleSize = 50;

    public static InferredColumnType Infer(IEnumerable<string> values)
    {
        var sample = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Take(SampleSize)
            .ToList();

        if (!sample.Any())
            return InferredColumnType.Unknown;

        // Boolean first (strict check)
        if (sample.All(v =>
            bool.TryParse(v, out _)))
        {
            return InferredColumnType.Boolean;
        }

        // DateTime
        if (sample.All(v =>
            DateTime.TryParse(
                v,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _)))
        {
            return InferredColumnType.DateTime;
        }

        // Numeric — parse via the shared NumericParser so that "is this
        // numeric?" here and the value computed in ColumnStatisticsCalculator
        // can never disagree (same culture, same number styles).
        if (sample.All(v => NumericParser.TryParse(v, out _)))
        {
            return InferredColumnType.Numeric;
        }

        // High uniqueness → likely text identifier
        var uniqueRatio = sample.Distinct().Count() / (decimal)sample.Count;

        if (uniqueRatio > 0.9m)
            return InferredColumnType.Text;

        return InferredColumnType.Categorical;
    }
}