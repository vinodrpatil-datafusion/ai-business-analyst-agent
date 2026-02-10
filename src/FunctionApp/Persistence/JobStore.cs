using Contracts.Invocation;
using Microsoft.Data.SqlClient;

namespace FunctionApp.Persistence;

public sealed class JobStore
{
    private readonly string _connectionString;

    public JobStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    /*------------------------------------------------------------
      CreateJobAsync
      ------------------------------------------------------------
      Creates a new job anchored to a BlobPath.
      Status is initialized to Pending.
    ------------------------------------------------------------*/
    public async Task CreateJobAsync(
        Guid jobId,
        SubmitJobRequestV1 request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO Jobs
            (JobId, BlobPath, Status, SubmittedAt, LastUpdatedAt)
            VALUES
            (@JobId, @BlobPath, @Status, @SubmittedAt, @LastUpdatedAt)";

        var now = DateTimeOffset.UtcNow;

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue("@BlobPath", request.BlobPath);
        cmd.Parameters.AddWithValue("@Status", "Pending");
        cmd.Parameters.AddWithValue("@SubmittedAt", now);
        cmd.Parameters.AddWithValue("@LastUpdatedAt", now);

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /*------------------------------------------------------------
      UpdateStatusAsync
      ------------------------------------------------------------
      Updates job status during processing lifecycle.
      ------------------------------------------------------------*/
    public async Task UpdateStatusAsync(
        Guid jobId,
        string status,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE Jobs
            SET Status = @Status,
                LastUpdatedAt = @LastUpdatedAt
            WHERE JobId = @JobId";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@LastUpdatedAt", DateTimeOffset.UtcNow);

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /*------------------------------------------------------------
      GetStatusAsync
      ------------------------------------------------------------
      Read-only query used by GetJobStatus API.
      Returns a stable contract for UI & orchestration.
    ------------------------------------------------------------*/
    public async Task<JobStatusResponseV1> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                j.JobId,
                j.Status,
                j.LastUpdatedAt,
                CASE
                    WHEN bi.JobId IS NOT NULL THEN CAST(1 AS BIT)
                    ELSE CAST(0 AS BIT)
                END AS InsightsAvailable
            FROM Jobs j
            LEFT JOIN BusinessInsights bi
                ON j.JobId = bi.JobId
            WHERE j.JobId = @JobId";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(cancellationToken);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            // Job not found → treat as not found
            return new JobStatusResponseV1(
                JobId: jobId,
                Status: "NotFound",
                LastUpdatedAt: DateTimeOffset.UtcNow,
                InsightsAvailable: false
            );
        }

        return new JobStatusResponseV1(
            JobId: reader.GetGuid(0),
            Status: reader.GetString(1),
            LastUpdatedAt: reader.GetDateTimeOffset(2),
            InsightsAvailable: reader.GetBoolean(3)
        );
    }


    public async Task<string?> GetBlobPathAsync(
    Guid jobId,
    CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT BlobPath
        FROM Jobs
        WHERE JobId = @JobId";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(cancellationToken);
        return (string?)await cmd.ExecuteScalarAsync(cancellationToken);
    }

}