namespace Jobby.Postgres;

internal class PostgresqlStorageSettings
{
    public string SchemaName { get; init; } = string.Empty;
    public string TablesPrefix { get; init; } = "jobby_";
    public SequenceFailureBehavior SequenceFailureBehavior { get; init; } = SequenceFailureBehavior.Block;
}
