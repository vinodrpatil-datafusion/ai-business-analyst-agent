using System.Text;
namespace FunctionApp.Parsing;

public sealed class CsvFileParser : IFileParser
{
    public async Task<IReadOnlyList<IDictionary<string, string>>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var firstLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(firstLine))
            return Array.Empty<IDictionary<string, string>>();

        var delimiter = DetectDelimiter(firstLine);

        var headers = firstLine.Split(delimiter)
                               .Select(h => h.Trim())
                               .ToArray();

        var rows = new List<IDictionary<string, string>>();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = line.Split(delimiter);

            var dict = new Dictionary<string, string>();

            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                dict[headers[i]] = values[i].Trim();
            }

            rows.Add(dict);
        }

        return rows;
    }

    private static char DetectDelimiter(string line)
    {
        if (line.Contains(';')) return ';';
        if (line.Contains('\t')) return '\t';
        return ',';
    }
}