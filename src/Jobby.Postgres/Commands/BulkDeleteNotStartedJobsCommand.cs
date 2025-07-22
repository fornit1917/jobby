using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkDeleteNotStartedJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;

    public BulkDeleteNotStartedJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _deleteCommandText = @$"
            DELETE FROM {TableName.Jobs(settings)}
            WHERE
                id = ANY($1)
                AND (status={(int)JobStatus.Scheduled} OR status={(int)JobStatus.WaitingPrev})
        ";
    }

    public async Task ExecuteAsync(IReadOnlyList<Guid> jobIds)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn)
        {
            Parameters =
                {
                    new() { Value = jobIds }
                }
        };
        await deleteCmd.ExecuteNonQueryAsync();
    }

    public void Execute(IReadOnlyList<Guid> jobIds)
    {
        using var conn = _dataSource.OpenConnection();
        using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn)
        {
            Parameters =
                {
                    new() { Value = jobIds }
                }
        };
        deleteCmd.ExecuteNonQuery();
    }
}
