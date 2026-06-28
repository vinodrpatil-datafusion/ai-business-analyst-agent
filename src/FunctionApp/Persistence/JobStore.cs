using Contracts.Invocation;
using Contracts.Jobs;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FunctionApp.Persistence;

/// <summary>
/// Provides persistence operations for job lifecycle management.
/// This class is responsible for creating jobs, transitioning states,
/// and retrieving job metadata in a concurrency-safe manner.
/// </summary>
public sealed class JobStore
{
    /// <summary>
    /// Maximum number of processing attempts before a Failed job stops being
    /// retryable. A job may be re-locked from Failed while RetryCount is below
    /// this cap; each failure increments RetryCount. With the default of 3,
    /// a job gets an initial attempt plus up to two retries before further
    /// reprocessing is refused (returns 409). Caps queue-trigger retry loops.
    /// </summary>
    public const int MaxProcessingAttempts = 3;

    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobStore"/> class.
    /// </summary>
    /// <param name="connectionString">
    /// The SQL connection string used to access the Jobs table.
    /// </param>
    public JobStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates a new job anchored to a BlobPath.
    /// Initializes status as <see cref="JobStatuses.Pending"/>.
    /// </summary>
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

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@BlobPath", SqlDbType.NVarChar, 512).Value = request.BlobPath;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = JobStatuses.Pending;
        cmd.Parameters.Add("@SubmittedAt", SqlDbType.DateTimeOffset).Value = now;
        cmd.Parameters.Add("@LastUpdatedAt", SqlDbType.DateTimeOffset).Value = now;

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Updates the job status and refreshes the LastUpdatedAt timestamp.
    /// </summary>
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

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = status;
        cmd.Parameters.Add("@LastUpdatedAt", SqlDbType.DateTimeOffset).Value = DateTimeOffset.UtcNow;

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves job status information for API consumption.
    /// Returns <c>null</c> if job does not exist.
    /// </summary>
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
                WHEN EXISTS (
                    SELECT 1 FROM BusinessInsights bi
                    WHERE bi.JobId = j.JobId
                ) THEN CAST(1 AS BIT)
                ELSE CAST(0 AS BIT)
            END AS InsightsAvailable
        FROM Jobs j
        WHERE j.JobId = @JobId";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;

        await conn.OpenAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new JobStatusResponseV1(
            JobId: reader.GetGuid(0),
            Status: reader.GetString(1),
            SubmittedAt: reader.GetDateTimeOffset(2),
            LastUpdatedAt: reader.GetDateTimeOffset(3),
            InsightsAvailable: reader.GetBoolean(4)
        );
    }

    /// <summary>
    /// Atomically transitions a job into Processing. Succeeds from either
    /// Pending (first attempt) or Failed (retry) — the latter only while
    /// RetryCount is below <see cref="MaxProcessingAttempts"/>. The atomic,
    /// guarded UPDATE means only one caller can win the lock (concurrency
    /// protection), and a permanently-failing job stops being retryable once
    /// the cap is reached.
    /// </summary>
    public async Task<bool> TryMarkProcessingAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = @ProcessingStatus,
            ProcessingStartedAt = SYSDATETIMEOFFSET(),
            LastUpdatedAt = SYSDATETIMEOFFSET()
        WHERE JobId = @JobId
          AND (
                Status = @PendingStatus
             OR (Status = @FailedStatus AND RetryCount < @MaxAttempts)
          );";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@ProcessingStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Processing;
        cmd.Parameters.Add("@PendingStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Pending;
        cmd.Parameters.Add("@FailedStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Failed;
        cmd.Parameters.Add("@MaxAttempts", SqlDbType.Int).Value = MaxProcessingAttempts;

        await conn.OpenAsync(cancellationToken);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);

        return affected == 1;
    }

    /// <summary>
    /// Marks job as Completed and records execution duration.
    /// </summary>
    public async Task MarkCompletedAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = @CompletedStatus,
            ProcessingCompletedAt = SYSDATETIMEOFFSET(),
            ProcessingDurationMs = DATEDIFF(
                MILLISECOND,
                ProcessingStartedAt,
                SYSDATETIMEOFFSET()
            ),
            LastUpdatedAt = SYSDATETIMEOFFSET()
        WHERE JobId = @JobId;";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@CompletedStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Completed;

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Marks job as Failed and records execution duration if started.
    /// </summary>
    public async Task MarkFailedAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        UPDATE Jobs
        SET Status = @FailedStatus,
            RetryCount = RetryCount + 1,
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

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@FailedStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Failed;

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves the BlobPath associated with a job.
    /// </summary>
    public async Task<string?> GetBlobPathAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT BlobPath
        FROM Jobs
        WHERE JobId = @JobId";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;

        await conn.OpenAsync(cancellationToken);

        return (string?)await cmd.ExecuteScalarAsync(cancellationToken);
    }
}