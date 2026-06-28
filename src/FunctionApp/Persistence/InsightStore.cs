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
    BusinessInsightsV1 structuredInsights,
    string? summaryJson,
    string rawResponse,
    DateTimeOffset generatedAt,
    string promptVersion,
    string modelDeployment,
    int? inputTokens,
    int? outputTokens,
    int? totalTokens,
    CancellationToken ct)
    {
        // Append-only: one row per processing attempt. Attempt is derived from
        // the count of existing rows for this job. This is safe because the
        // per-job processing lock (JobStore.TryMarkProcessingAsync) guarantees
        // only one writer per JobId at a time, so the count cannot race.
        const string sql = @"
            DECLARE @Attempt INT =
                (SELECT COUNT(*) + 1 FROM BusinessInsights WHERE JobId = @JobId);

            INSERT INTO BusinessInsights
            (
                JobId,
                Attempt,
                InsightsJson,
                SummaryJson,
                RawResponse,
                GeneratedAt,
                PromptVersion,
                ModelDeployment,
                InputTokens,
                OutputTokens,
                TotalTokens
            )
            VALUES
            (
                @JobId,
                @Attempt,
                @InsightsJson,
                @SummaryJson,
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
        cmd.Parameters.AddWithValue("@SummaryJson",
            (object?)summaryJson ?? DBNull.Value);
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

    /// <summary>
    /// Returns the most recent persisted <see cref="BusinessInsightsV1"/> for a
    /// job (latest attempt by GeneratedAt), or <c>null</c> if none exist yet.
    /// </summary>
    public async Task<BusinessInsightsV1?> GetByJobIdAsync(
        Guid jobId,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 InsightsJson
            FROM BusinessInsights
            WHERE JobId = @JobId
            ORDER BY GeneratedAt DESC, Id DESC";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(ct);

        var json = (string?)await cmd.ExecuteScalarAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<BusinessInsightsV1>(json);
    }
}
