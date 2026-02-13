namespace FunctionApp.Analysis;

/// <summary>
/// Validates minimum schema requirements before analysis.
/// Prevents invalid or malformed files from being processed.
/// </summary>
public static class SchemaValidator
{
    public static void Validate(
        IReadOnlyList<IDictionary<string, string>> rows)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("File contains no data rows.");

        var headers = rows.First().Keys;

        if (!headers.Any())
            throw new InvalidOperationException("No headers detected.");

        if (headers.Any(h => string.IsNullOrWhiteSpace(h)))
            throw new InvalidOperationException("Empty column name detected.");
    }
}
