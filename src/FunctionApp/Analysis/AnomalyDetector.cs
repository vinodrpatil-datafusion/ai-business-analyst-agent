namespace FunctionApp.Analysis;

/// <summary>
/// Detects numeric outliers using Z-score based logic.
/// Only flags statistically significant anomalies.
/// </summary>
public static class AnomalyDetector
{
    public static IEnumerable<string> DetectNumericOutliers(
        string columnName,
        IEnumerable<decimal> values)
    {
        var list = values.ToList();
        if (list.Count < 5)
            yield break;

        var mean = list.Average();

        var variance = list.Average(v =>
            Math.Pow((double)(v - mean), 2));

        var stdDev = (decimal)Math.Sqrt(variance);

        if (stdDev == 0)
            yield break;

        foreach (var value in list)
        {
            if (Math.Abs(value - mean) > 3 * stdDev)
            {
                yield return
                    $"{columnName} outlier detected: {value}";
            }
        }
    }
}
