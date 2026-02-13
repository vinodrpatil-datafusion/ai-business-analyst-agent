using Microsoft.Extensions.Logging;

namespace FunctionApp.Parsing;

/// <summary>
/// Factory responsible for selecting the appropriate file parser
/// based on file extension.
/// 
/// This keeps SignalExtractionAgent focused on orchestration
/// and prevents format-specific logic from leaking into agents.
/// </summary>
public sealed class FileParserFactory
{
    private readonly CsvFileParser _csvParser;
    private readonly ExcelFileParser _excelParser;
    private readonly ILogger<FileParserFactory> _logger;

    public FileParserFactory(
        CsvFileParser csvParser,
        ExcelFileParser excelParser,
        ILogger<FileParserFactory> logger)
    {
        _csvParser = csvParser;
        _excelParser = excelParser;
        _logger = logger;
    }

    /// <summary>
    /// Creates an appropriate parser instance based on file extension.
    /// </summary>
    /// <param name="blobPath">The blob path or filename.</param>
    /// <returns>IFileParser implementation.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when file extension is unsupported.
    /// </exception>
    public IFileParser Create(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            throw new ArgumentException(
                "Blob path cannot be null or empty.",
                nameof(blobPath));

        var extension = Path.GetExtension(blobPath)
                            ?.ToLowerInvariant();

        _logger.LogInformation(
            "Selecting parser for extension {Extension}",
            extension);

        return extension switch
        {
            ".csv" => _csvParser,
            ".xlsx" => _excelParser,
            ".xls" => _excelParser, // Legacy Excel support
            _ => throw new NotSupportedException(
                $"File type '{extension}' is not supported.")
        };
    }
}