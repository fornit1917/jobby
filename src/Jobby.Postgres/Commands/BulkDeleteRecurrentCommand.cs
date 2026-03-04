using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkDeleteRecurrentCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;

    public BulkDeleteRecurrentCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _deleteCommandText = $@"DELETE FROM {DbName.Jobs(settings)} WHERE id = ANY($1) AND schedule IS NOT NULL";
    }

    public async Task ExecuteAsync(IReadOnlyList<Guid> jobIds)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn);
        deleteCmd.Parameters.Add(new() { Value = jobIds });
        await deleteCmd.ExecuteNonQueryAsync();
    }

    public void Execute(IReadOnlyList<Guid> jobIds)
    {
        using var conn = _dataSource.OpenConnection();
        using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn);
        deleteCmd.Parameters.Add(new() { Value = jobIds });
        deleteCmd.ExecuteNonQuery();
    }
}