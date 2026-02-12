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
    object structuredInsights,
    string rawResponse,
    DateTimeOffset generatedAt,
    string promptVersion,
    string modelDeployment,
    int? inputTokens,
    int? outputTokens,
    int? totalTokens,
    CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO BusinessInsights (
                JobId,
                InsightsJson,
                RawResponse,
                GeneratedAt,
                PromptVersion,
                ModelDeployment,
                InputTokens,
                OutputTokens,
                TotalTokens
            )
            VALUES (
                @JobId,
                @InsightsJson,
                @RawResponse,
                @GeneratedAt,
                @PromptVersion,
                @ModelDeployment,
                @InputTokens,
                @OutputTokens,
                @TotalTokens
            )";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue("@InsightsJson",
            JsonSerializer.Serialize(structuredInsights));
        cmd.Parameters.AddWithValue("@RawResponse", rawResponse ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@GeneratedAt", generatedAt);
        cmd.Parameters.AddWithValue("@PromptVersion", promptVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ModelDeployment", modelDeployment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@InputTokens", inputTokens ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@OutputTokens", outputTokens ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TotalTokens", totalTokens ?? (object)DBNull.Value);

        await conn.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
