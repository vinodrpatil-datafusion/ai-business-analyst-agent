using System.Text;

namespace FunctionApp.Parsing;

/// <summary>
/// RFC 4180-style CSV parser.
///
/// Replaces the previous <c>line.Split(delimiter)</c> implementation, which
/// corrupted any field containing the delimiter inside quotes (e.g.
/// "Smith, John") and could not represent embedded newlines. This parser
/// handles:
///   - quoted fields containing the delimiter,
///   - escaped quotes ("" inside a quoted field => a literal "),
///   - newlines embedded inside quoted fields,
///   - CRLF, LF and lone-CR line endings.
///
/// The delimiter is auto-detected from the header line (',', ';' or tab),
/// preserving the original behaviour (',' vs ';' matters for many European
/// CSV exports).
/// </summary>
public sealed class CsvFileParser : IFileParser
{
    public async Task<IReadOnlyList<IDictionary<string, string>>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<IDictionary<string, string>>();

        var delimiter = DetectDelimiter(content);

        var records = ParseRecords(content, delimiter);
        if (records.Count == 0)
            return Array.Empty<IDictionary<string, string>>();

        var headers = records[0].Select(h => h.Trim()).ToArray();

        var rows = new List<IDictionary<string, string>>(records.Count - 1);

        for (int r = 1; r < records.Count; r++)
        {
            var fields = records[r];

            // Skip blank lines (tokenized as a single empty field).
            if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]))
                continue;

            var dict = new Dictionary<string, string>(headers.Length);

            for (int i = 0; i < headers.Length && i < fields.Count; i++)
                dict[headers[i]] = fields[i].Trim();

            rows.Add(dict);
        }

        return rows;
    }

    private static char DetectDelimiter(string content)
    {
        // Inspect only the first physical line for delimiter detection.
        var newline = content.IndexOfAny(new[] { '\r', '\n' });
        var firstLine = newline >= 0 ? content[..newline] : content;

        if (firstLine.Contains(';')) return ';';
        if (firstLine.Contains('\t')) return '\t';
        return ',';
    }

    /// <summary>
    /// Single-pass state machine. A double quote toggles "in-quotes" mode;
    /// a doubled quote inside a quoted field is an escaped literal quote.
    /// Delimiters and newlines are only structural when not inside quotes.
    /// </summary>
    private static List<List<string>> ParseRecords(string content, char delimiter)
    {
        var records = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Doubled quote => literal quote; otherwise end of quoted field.
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                current.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r')
            {
                if (i + 1 < content.Length && content[i + 1] == '\n')
                    i++;

                current.Add(field.ToString());
                field.Clear();
                records.Add(current);
                current = new List<string>();
            }
            else if (c == '\n')
            {
                current.Add(field.ToString());
                field.Clear();
                records.Add(current);
                current = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        // Flush the final field/record when the file has no trailing newline.
        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            records.Add(current);
        }

        return records;
    }
}
