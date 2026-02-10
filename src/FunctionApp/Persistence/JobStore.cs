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

    public async Task CreateJobAsync(
        Guid jobId,
        SubmitJobRequestV1 request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO Jobs
            (JobId, FileName, FileType, Status, SubmittedAt, LastUpdatedAt)
            VALUES
            (@JobId, @FileName, @FileType, @Status, @SubmittedAt, @LastUpdatedAt)";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@JobId", jobId);
        cmd.Parameters.AddWithValue("@FileName", request.FileName);
        cmd.Parameters.AddWithValue("@FileType", request.FileType);
        cmd.Parameters.AddWithValue("@Status", "Pending");
        cmd.Parameters.AddWithValue("@SubmittedAt", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("@LastUpdatedAt", DateTimeOffset.UtcNow);

        await conn.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
