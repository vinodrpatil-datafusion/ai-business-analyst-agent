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
        const string sql = @"
            INSERT INTO BusinessSignals
            (JobId, SignalsJson, GeneratedAt)
            VALUES
            (@JobId, @SignalsJson, @GeneratedAt)";

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
