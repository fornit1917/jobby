using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class FindAndRestartStuckJobsCommand
{
    private string _commandText;

    public FindAndRestartStuckJobsCommand(PostgresqlStorageSettings settings)
    {
        _commandText = $@"
            WITH restarted AS (
                UPDATE {TableName.Jobs(settings)}  
                SET 
                    status = {(int)JobStatus.Scheduled},
                    started_count = started_count - 1
                WHERE
                    status = {(int)JobStatus.Processing}  
                    AND can_be_restarted = TRUE
                    AND server_id = ANY($1)
                RETURNING
                    id, job_name, server_id, can_be_restarted
            )

            SELECT * FROM restarted

            UNION 

            SELECT id, job_name, server_id, can_be_restarted
            FROM {TableName.Jobs(settings)}
            WHERE 
                status = {(int)JobStatus.Processing}
                AND can_be_restarted = FALSE
                AND server_id = ANY($1);
		";
    }

	public async Task ExecuteInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction? tr, IReadOnlyList<string> lostServerIds, List<StuckJobModel> stuckJobs)
	{
		stuckJobs.Clear();

        await using var cmd = new NpgsqlCommand(_commandText, conn, tr)
        {
            Parameters =
            {
                new() { Value = lostServerIds }
            }
        };

		await using var reader = await cmd.ExecuteReaderAsync();
        while (true)
        {
            var job = await reader.GetStuckJobAsync();
            if (job == null)
            {
                return;
            }

            stuckJobs.Add(job);
        }
    }
}
