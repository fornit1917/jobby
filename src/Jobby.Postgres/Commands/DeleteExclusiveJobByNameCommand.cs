using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class DeleteExclusiveJobByNameCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public DeleteExclusiveJobByNameCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _commandText = $@"
            DELETE FROM {DbName.Jobs(settings)}
            WHERE job_name = $1 AND is_exclusive = true;
        ";
    }

    public async Task ExecuteAsync(string jobName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = PrepareCommand(conn, jobName);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Execute(string jobName)
    {
        using var conn = _dataSource.OpenConnection();
        using var cmd = PrepareCommand(conn, jobName);
        cmd.ExecuteNonQuery();
    }

    private NpgsqlCommand PrepareCommand(NpgsqlConnection conn, string jobName)
    {
        return new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = jobName }
            }
        };
    }
}
