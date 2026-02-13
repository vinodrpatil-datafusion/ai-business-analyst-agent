namespace FunctionApp.Analysis;

/// <summary>
/// Computes numeric statistics deterministically.
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
        var numbers = values
            .Where(v => decimal.TryParse(v, out _))
            .Select(decimal.Parse)
            .ToList();

        if (!numbers.Any())
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
