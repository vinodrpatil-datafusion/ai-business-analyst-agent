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
                WHEN bi.JobId IS NOT NULL THEN CAST(1 AS BIT)
                ELSE CAST(0 AS BIT)
            END AS InsightsAvailable
        FROM Jobs j
        LEFT JOIN BusinessInsights bi
            ON j.JobId = bi.JobId
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
    /// Atomically transitions job from Pending → Processing.
    /// Only one caller may succeed (concurrency protection).
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
          AND Status = @PendingStatus;";

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add("@JobId", SqlDbType.UniqueIdentifier).Value = jobId;
        cmd.Parameters.Add("@ProcessingStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Processing;
        cmd.Parameters.Add("@PendingStatus", SqlDbType.NVarChar, 50).Value = JobStatuses.Pending;

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