using Contracts.Signals;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace FunctionApp.Persistence;

public sealed class SignalStore
{
    private readonly string _connectionString;

    public SignalStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveAsync(
        Guid jobId,
        BusinessSignalsV1 signals,
        CancellationToken cancellationToken)
    {
        // Append-only: one row per processing attempt. Attempt derives from the
        // existing row count; safe because the per-job processing lock
        // serializes writers for a given JobId (see InsightStore for rationale).
        const string sql = @"
            DECLARE @Attempt INT =
                (SELECT COUNT(*) + 1 FROM BusinessSignals WHERE JobId = @JobId);

            INSERT INTO BusinessSignals
            (JobId, Attempt, SignalsJson, GeneratedAt)
            VALUES
            (@JobId, @Attempt, @SignalsJson, @GeneratedAt)";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue(
            "@SignalsJson",
            JsonSerializer.Serialize(signals)
        );
        cmd.Parameters.AddWithValue("@GeneratedAt", DateTimeOffset.UtcNow);

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
