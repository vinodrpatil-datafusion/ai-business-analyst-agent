namespace FunctionApp.Analysis;

/// <summary>
/// Computes numeric statistics deterministically.
///
/// Parses through the shared <see cref="NumericParser"/> so that the set of
/// values treated as numeric here is identical to the set
/// <see cref="ColumnTypeInference"/> used to classify the column. A single
/// pass replaces the previous parse-twice (TryParse then Parse) approach.
/// </summary>
public static class ColumnStatisticsCalculator
{
    public static (
        decimal? Average,
        decimal? Total,
        decimal? Min,
        decimal? Max,
        List<decimal> ParsedValues)
        ComputeNumeric(IEnumerable<string> values)
    {
        var numbers = new List<decimal>();

        foreach (var v in values)
        {
            if (NumericParser.TryParse(v, out var n))
                numbers.Add(n);
        }

        if (numbers.Count == 0)
            return (null, null, null, null, new List<decimal>());

        return (
            Average: numbers.Average(),
            Total: numbers.Sum(),
            Min: numbers.Min(),
            Max: numbers.Max(),
            ParsedValues: numbers
        );
    }
}
