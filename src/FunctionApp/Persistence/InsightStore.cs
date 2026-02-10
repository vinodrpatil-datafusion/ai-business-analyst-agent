using Contracts.Insights;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace FunctionApp.Persistence;

public sealed class InsightStore
{
    private readonly string _connectionString;

    public InsightStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveAsync(
        Guid jobId,
        BusinessInsightsV1 insights,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO BusinessInsights
            (JobId, InsightsJson, GeneratedAt)
            VALUES
            (@JobId, @InsightsJson, @GeneratedAt)";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue(
            "@InsightsJson",
            JsonSerializer.Serialize(insights)
        );
        cmd.Parameters.AddWithValue("@GeneratedAt", DateTimeOffset.UtcNow);

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
