using Jobby.Core.Models;

namespace Jobby.Postgres.Helpers;

internal static class GroupLockSql
{
    public static string StuckLocker(string alias, PostgresqlStorageSettings settings) => $@"
        {alias}.is_group_locker = TRUE
        AND (
            {alias}.status = {(int)JobStatus.Failed}
            OR (
                {alias}.status = {(int)JobStatus.Processing}
                AND {alias}.can_be_restarted = FALSE
                AND NOT EXISTS (SELECT 1 FROM {DbName.Servers(settings)} s WHERE s.id = {alias}.server_id)
            )
        )";
}