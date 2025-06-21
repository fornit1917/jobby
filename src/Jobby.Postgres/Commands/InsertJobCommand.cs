using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class InsertJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public InsertJobCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        
        _commandText = @$"
            INSERT INTO {TableName.Jobs(settings)} (
                id,
                job_name,
                job_param,
                status,
                created_at,
                scheduled_start_at,
                cron,
                next_job_id,
                can_be_restarted
            )
            VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8,
                $9
            )
            ON CONFLICT (job_name) WHERE cron IS NOT null DO 
            UPDATE SET
                id = $1,
                job_param = $3,
	            cron = $7,
	            scheduled_start_at = $6,
                can_be_restarted = $9;
        ";
    }

    private NpgsqlCommand CreateCommand(NpgsqlConnection conn, JobCreationModel job)
    {
        return new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = job.Id },                                           // 1
                new() { Value = job.JobName },                                      // 2
                new() { Value = (object?)job.JobParam ?? DBNull.Value },            // 3
                new() { Value = (int)job.Status },                                  // 4
                new() { Value = job.CreatedAt },                                    // 5
                new() { Value = job.ScheduledStartAt },                             // 6
                new() { Value = (object?)job.Cron ?? DBNull.Value },                // 7
                new() { Value = (object?)job.NextJobId ?? DBNull.Value },           // 8
                new() { Value = job.CanBeRestarted },                               // 9
            }
        };
    }

    public async Task ExecuteAsync(JobCreationModel job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = CreateCommand(conn, job);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Execute(JobCreationModel job)
    {
        using var conn = _dataSource.OpenConnection();
        using var cmd = CreateCommand(conn, job);
        cmd.ExecuteNonQuery();
    }
}
