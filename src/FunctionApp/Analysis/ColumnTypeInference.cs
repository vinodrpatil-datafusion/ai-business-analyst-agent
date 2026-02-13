namespace FunctionApp.Analysis;

/// <summary>
/// Infers column data types based on sampled values.
/// Supports Numeric, Date, and Categorical types.
/// </summary>
public static class ColumnTypeInference
{
    public static InferredColumnType Infer(IEnumerable<string> values)
    {
        var sample = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Take(50)
            .ToList();

        if (!sample.Any())
            return InferredColumnType.Unknown;

        if (sample.All(v => decimal.TryParse(v, out _)))
            return InferredColumnType.Numeric;

        if (sample.All(v => DateTime.TryParse(v, out _)))
            return InferredColumnType.Date;

        return InferredColumnType.Categorical;
    }
}

public enum InferredColumnType
{
    Numeric,
    Date,
    Categorical,
    Unknown
}
