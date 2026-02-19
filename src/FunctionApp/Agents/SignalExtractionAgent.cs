using Contracts.Signals;
using FunctionApp.Analysis;
using FunctionApp.Parsing;
using Microsoft.Extensions.Logging;

namespace FunctionApp.Agents;

/// <summary>
/// Deterministic signal extraction agent.
/// Responsible for:
/// - Reading structured files from Blob Storage
/// - Inferring schema & column types
/// - Computing statistical metadata
/// - Detecting anomalies
///
/// Raw data is NEVER passed directly to the LLM.
/// Only structured, deterministic signals are returned.
/// </summary>
public sealed class SignalExtractionAgent
    : IAgent<string, BusinessSignalsV1>
{
    private readonly BlobFileReader _blobReader;
    private readonly FileParserFactory _parserFactory;
    private readonly ILogger<SignalExtractionAgent> _logger;

    public SignalExtractionAgent(
        BlobFileReader blobReader,
        FileParserFactory parserFactory,
        ILogger<SignalExtractionAgent> logger)
    {
        _blobReader = blobReader;
        _parserFactory = parserFactory;
        _logger = logger;
    }

    public async Task<BusinessSignalsV1> ExecuteAsync(
        string blobPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            throw new ArgumentException("BlobPath cannot be empty.", nameof(blobPath));

        _logger.LogInformation(
            "Starting deterministic signal extraction for BlobPath {BlobPath}",
            blobPath);

        const string container = "uploads";

        // ------------------------------------------------------------
        // 1. Read file stream from Blob Storage
        // ------------------------------------------------------------
        await using var stream = await _blobReader.ReadStreamAsync(
            container,
            blobPath,
            cancellationToken);

        var parser = _parserFactory.Create(blobPath);

        var rows = await parser.ParseAsync(stream, cancellationToken);

        SchemaValidator.Validate(rows);

        var recordCount = rows.Count;

        _logger.LogInformation(
            "Parsed {RecordCount} records from {BlobPath}",
            recordCount,
            blobPath);

        if (recordCount == 0)
            throw new InvalidOperationException("Parsed file contains zero rows.");

        // ------------------------------------------------------------
        // 2. Sequential column-level deterministic analysis
        // ------------------------------------------------------------
        var numericTotals = new Dictionary<string, decimal>();
        var numericAverages = new Dictionary<string, decimal>();
        var categoryCounts = new Dictionary<string, int>();
        var metadata = new Dictionary<string, ColumnMetadataV1>();
        var anomalies = new List<string>();

        var firstRow = rows.First();

        foreach (var column in firstRow.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = rows
                .Select(r => r.TryGetValue(column, out var v) ? v : string.Empty)
                .ToList();

            var inferredType = ColumnTypeInference.Infer(values);

            var nullCount = values.Count(v =>
                string.IsNullOrWhiteSpace(v));

            var uniqueCount = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .Count();

            if (inferredType == InferredColumnType.Numeric)
            {
                var stats = ColumnStatisticsCalculator
                    .ComputeNumeric(values);

                if (stats.Total.HasValue)
                    numericTotals[column] = stats.Total.Value;

                if (stats.Average.HasValue)
                    numericAverages[column] = stats.Average.Value;

                foreach (var anomaly in AnomalyDetector
                    .DetectNumericOutliers(column, stats.ParsedValues))
                {
                    anomalies.Add(anomaly);
                }

                metadata[column] = new ColumnMetadataV1(
                    ColumnName: column,
                    ColumnType: InferredColumnType.Numeric,
                    NullCount: nullCount,
                    UniqueCount: uniqueCount,
                    Min: stats.Min,
                    Max: stats.Max,
                    Average: stats.Average);
            }
            else if (inferredType == InferredColumnType.Categorical)
            {
                foreach (var group in values
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .GroupBy(v => v))
                {
                    categoryCounts[$"{column}:{group.Key}"] =
                        group.Count();
                }

                metadata[column] = new ColumnMetadataV1(
                    ColumnName: column,
                    ColumnType: InferredColumnType.Categorical,
                    NullCount: nullCount,
                    UniqueCount: uniqueCount,
                    Min: null,
                    Max: null,
                    Average: null);
            }
            else
            {
                metadata[column] = new ColumnMetadataV1(
                    ColumnName: column,
                    ColumnType: inferredType,
                    NullCount: nullCount,
                    UniqueCount: uniqueCount,
                    Min: null,
                    Max: null,
                    Average: null);
            }
        }

        _logger.LogInformation(
            "Signal extraction completed for {BlobPath}. NumericColumns: {NumericCount}, AnomaliesDetected: {AnomalyCount}",
            blobPath,
            numericTotals.Count,
            anomalies.Count);

        return new BusinessSignalsV1(
            RecordCount: recordCount,
            GeneratedAt: DateTimeOffset.UtcNow,
            NumericAverages: numericAverages,
            NumericTotals: numericTotals,
            CategoryCounts: categoryCounts,
            ColumnMetadata: metadata,
            DetectedAnomalies: anomalies
        );
    }
}