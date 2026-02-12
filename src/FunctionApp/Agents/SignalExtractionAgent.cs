using Contracts.Signals;
using FunctionApp.Parsing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

namespace FunctionApp.Agents;

public sealed class SignalExtractionAgent
    : IAgent<string, BusinessSignalsV1>
{
    private readonly BlobFileReader _blobReader;
    private readonly ILogger<SignalExtractionAgent> _logger;

    private const string ContainerName = "uploads";

    public SignalExtractionAgent(
        BlobFileReader blobReader,
        ILogger<SignalExtractionAgent> logger)
    {
        _blobReader = blobReader;
        _logger = logger;
    }

    public async Task<BusinessSignalsV1> ExecuteAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting deterministic signal extraction for blob {BlobPath}",
            blobPath);

        try
        {
            var fileContent = await _blobReader.ReadTextAsync(
                ContainerName,
                blobPath,
                cancellationToken);

            var lines = fileContent
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
                throw new InvalidOperationException("File contains no data rows.");

            var headers = lines[0].Split(',');

            var numericTotals = new Dictionary<string, decimal>();
            var numericCounts = new Dictionary<string, int>();
            var categoryCounts = new Dictionary<string, int>();

            var recordCount = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');

                if (values.Length != headers.Length)
                    continue;

                recordCount++;

                for (int col = 0; col < headers.Length; col++)
                {
                    var header = headers[col].Trim();
                    var value = values[col].Trim();

                    // Numeric detection
                    if (decimal.TryParse(
                        value,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number))
                    {
                        numericTotals[header] =
                            numericTotals.TryGetValue(header, out var existing)
                                ? existing + number
                                : number;

                        numericCounts[header] =
                            numericCounts.TryGetValue(header, out var count)
                                ? count + 1
                                : 1;
                    }
                    else
                    {
                        // Basic category aggregation
                        var key = $"{header}:{value}";

                        categoryCounts[key] =
                            categoryCounts.TryGetValue(key, out var count)
                                ? count + 1
                                : 1;
                    }
                }
            }

            var numericAverages = numericTotals.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                    numericCounts.TryGetValue(kvp.Key, out var count) && count > 0
                        ? kvp.Value / count
                        : 0);

            stopwatch.Stop();

            _logger.LogInformation(
                "Signal extraction completed for blob {BlobPath} in {ElapsedMs} ms. Records: {Count}",
                blobPath,
                stopwatch.ElapsedMilliseconds,
                recordCount);

            return new BusinessSignalsV1(
                RecordCount: recordCount,
                NumericAverages: numericAverages,
                NumericTotals: numericTotals,
                CategoryCounts: categoryCounts,
                GeneratedAt: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Signal extraction failed for blob {BlobPath}",
                blobPath);

            throw; // Let ProcessJobFunction mark job as Failed
        }
    }
}