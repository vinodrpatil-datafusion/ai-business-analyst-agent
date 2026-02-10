using Contracts.Signals;

namespace FunctionApp.Agents;

public sealed class SignalExtractionAgent
    : IAgent<string, BusinessSignalsV1>
{
    public Task<BusinessSignalsV1> ExecuteAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // - Load file from Blob Storage
        // - Parse CSV / Excel
        // - Compute deterministic signals

        var signals = new BusinessSignalsV1(
            RecordCount: 0,
            NumericAverages: new Dictionary<string, decimal>(),
            NumericTotals: new Dictionary<string, decimal>(),
            CategoryCounts: new Dictionary<string, int>()
        );

        return Task.FromResult(signals);
    }
}
