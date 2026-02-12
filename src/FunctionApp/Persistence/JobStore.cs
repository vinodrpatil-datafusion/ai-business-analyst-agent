using Contracts.Invocation;
using Microsoft.Data.SqlClient;
using System.Data;

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
    public async Task<JobStatusResponseV1?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT
            j.JobId,
            j.Status,
            j.SubmittedAt,
            j.LastUpdatedAt,
            CASE
                WHEN bi.JobId IS NOT NULL THEN CAST(1 AS BIT)
                ELSE CAST(0 AS BIT)
            END AS InsightsAvailable
        FROM Jobs j
        LEFT JOIN BusinessInsights bi
            ON j.JobId = bi.JobId
        WHERE j.JobId = @JobId";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier)
                      .Value = jobId;

        await conn.OpenAsync(cancellationToken);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null; // Let API layer return 404
        }

        return new JobStatusResponseV1(
            JobId: reader.GetGuid(0),
            Status: reader.GetString(1),
            SubmittedAt: reader.GetDateTimeOffset(2),
            LastUpdatedAt: reader.GetDateTimeOffset(3),
            InsightsAvailable: reader.GetBoolean(4)
        );
    }

    /// <summary>
    /// Attempts to atomically mark the specified job as 'Processing' if its current status is 'Pending'.
    /// Only the caller that updates a single row will observe success; this method is used to implement
    /// a lightweight lease/race so only one worker proceeds to process the job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to mark as processing.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>
    /// <c>true</c> if the job status was updated (one row affected) and the caller may proceed with processing;
    /// otherwise <c>false</c> if the job was not in the expected 'Pending' state or another caller won the race.
    /// </returns>
    public async Task<bool> TryMarkProcessingAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = 'Processing',
            ProcessingStartedAt = SYSDATETIMEOFFSET(),
            LastUpdatedAt = SYSDATETIMEOFFSET()
        WHERE JobId = @JobId
          AND Status = 'Pending';";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(cancellationToken);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);

        return affected == 1;
    }

    public async Task MarkCompletedAsync(
    Guid jobId,
    CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = 'Completed',
            ProcessingCompletedAt = SYSDATETIMEOFFSET(),
            ProcessingDurationMs = DATEDIFF(
                MILLISECOND,
                ProcessingStartedAt,
                SYSDATETIMEOFFSET()
            ),
            LastUpdatedAt = SYSDATETIMEOFFSET()
        WHERE JobId = @JobId;";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(cancellationToken);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
    Guid jobId,
    CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = 'Failed',
            ProcessingCompletedAt = SYSDATETIMEOFFSET(),
            ProcessingDurationMs = CASE
                WHEN ProcessingStartedAt IS NOT NULL
                THEN DATEDIFF(
                    MILLISECOND,
                    ProcessingStartedAt,
                    SYSDATETIMEOFFSET()
                )
                ELSE NULL
            END,
            LastUpdatedAt = SYSDATETIMEOFFSET()
        WHERE JobId = @JobId;";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);

        await conn.OpenAsync(cancellationToken);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
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