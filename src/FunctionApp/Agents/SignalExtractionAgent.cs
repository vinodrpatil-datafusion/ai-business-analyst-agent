using Contracts.Signals;
using FunctionApp.Parsing;

namespace FunctionApp.Agents;

public sealed class SignalExtractionAgent
    : IAgent<string, BusinessSignalsV1>
{
    private readonly BlobFileReader _blobReader;

    public SignalExtractionAgent(BlobFileReader blobReader)
    {
        _blobReader = blobReader;
    }

    public async Task<BusinessSignalsV1> ExecuteAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        // Temporary convention for testing
        const string containerName = "uploads";

        var fileContent = await _blobReader.ReadTextAsync(
            containerName,
            blobPath,
            cancellationToken);

        // TODO: Replace with real CSV/Excel parsing
        // For now, return placeholder signals

        return new BusinessSignalsV1(
            RecordCount: 100,
            NumericAverages: new Dictionary<string, decimal>
            {
                ["Revenue"] = 1250.50m
            },
            NumericTotals: new Dictionary<string, decimal>
            {
                ["Revenue"] = 125050m
            },
            CategoryCounts: new Dictionary<string, int>
            {
                ["Region:EMEA"] = 40,
                ["Region:APAC"] = 60
            }
        );
    }
}
