using ExcelDataReader;

namespace FunctionApp.Parsing;

public sealed class ExcelFileParser : IFileParser
{
    public async Task<IReadOnlyList<IDictionary<string, string>>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        System.Text.Encoding.RegisterProvider(
            System.Text.CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(stream);

        var result = reader.AsDataSet();
        var table = result.Tables[0];

        var headers = table.Rows[0]
            .ItemArray
            .Select(x => x?.ToString() ?? "")
            .ToArray();

        var rows = new List<IDictionary<string, string>>();

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var dict = new Dictionary<string, string>();

            for (int j = 0; j < headers.Length; j++)
            {
                dict[headers[j]] = row[j]?.ToString() ?? "";
            }

            rows.Add(dict);
        }

        return rows;
    }
}