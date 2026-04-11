using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres;

internal class PostgresqlPermanentLocksStorage : IPermanentLocksStorage
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _globalLockCommand;
    private readonly string _freezeCommand;
    private readonly string _unlockCommand;

    public PostgresqlPermanentLocksStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _globalLockCommand = "SELECT pg_advisory_xact_lock(hashtext('jobby_permanent_locks'))";

        _freezeCommand = $@"
            WITH 
                queue_top AS (
                    SELECT id, serializable_group_id, job_name FROM {DbName.Jobs(settings)}
                    WHERE
                        queue_name = $1 
                        AND status = {(int)JobStatus.Scheduled}
                    ORDER BY scheduled_start_at
                    LIMIT $2
                ),
                permanent_locked AS (
                    SELECT id, serializable_group_id, job_name FROM queue_top
                    WHERE
                        serializable_group_id IS NOT NULL 
                        AND EXISTS (
                            SELECT 1 FROM {DbName.Jobs(settings)} as locker
                            WHERE
                                locker.serializable_group_id = queue_top.serializable_group_id
                                AND locker.is_group_locker = true
                                AND locker.id != queue_top.id
                                AND (
                                    locker.status = {(int)JobStatus.Failed}
                                    OR (
                                        locker.status = {(int)JobStatus.Processing}
                                        AND locker.can_be_restarted = false
                                        AND NOT EXISTS (
                                            SELECT 1 FROM {DbName.Servers(settings)} as s
                                            WHERE s.id = locker.server_id
                                        )
                                    )
                                )
                        )
                        AND NOT EXISTS (
                            SELECT 1 FROM {DbName.UnlockingGroups(settings)} as u
                            WHERE u.group_id = queue_top.serializable_group_id
                            FOR UPDATE
                        )
                    FOR UPDATE SKIP LOCKED
                )
            UPDATE {DbName.Jobs(settings)}
            SET status = {(int)JobStatus.Frozen}
            WHERE id IN (SELECT id FROM permanent_locked)
            RETURNING id, serializable_group_id, job_name;
        ";

        _unlockCommand = $@"
            WITH
                unlocking_request AS (
                    SELECT group_id FROM {DbName.UnlockingGroups(settings)}
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                ),
                frozen_jobs AS (
                    SELECT id FROM {DbName.Jobs(settings)}
                    WHERE 
                        serializable_group_id IN (SELECT group_id FROM unlocking_request)
                        AND status = {(int)JobStatus.Frozen}
                    ORDER BY scheduled_start_at
                    LIMIT 100
                ),
                unfrozen AS (
                    UPDATE {DbName.Jobs(settings)}
                    SET status = {(int)JobStatus.Scheduled}
                    WHERE id IN (SELECT id FROM frozen_jobs)
                    RETURNING 1
                ),
                deleted_unlocking_request AS (
                    DELETE FROM {DbName.UnlockingGroups(settings)}
                    WHERE 
                        group_id IN (SELECT group_id FROM unlocking_request)
                        AND NOT EXISTS (SELECT 1 FROM unfrozen)
                    RETURNING group_id
                ),
                deleted_locker AS (
    	            DELETE FROM {DbName.Jobs(settings)}
    	            WHERE
                        serializable_group_id IS NOT NULL
                        AND is_group_locker = TRUE
    		            AND serializable_group_id IN (SELECT group_id FROM deleted_unlocking_request)
    	            RETURNING serializable_group_id
                )
            
            SELECT r.group_id, (d.serializable_group_id IS NOT NULL) is_unlocked FROM unlocking_request r
            LEFT JOIN deleted_locker d ON d.serializable_group_id = r.group_id;
        ";
    }

    public async Task FreezePermanentLockedJobsFromTopOfQueue(string queueName, int batchSize, List<JobWithGroupModel> frozenJobs)
    {
        frozenJobs.Clear();
        
        await using var conn = await _dataSource.OpenConnectionAsync();

        await using var tx = await conn.BeginTransactionAsync();
        
        await using var lockCmd = new NpgsqlCommand(_globalLockCommand, conn, tx);
        await lockCmd.ExecuteNonQueryAsync();
        
        await using var freezeCmd = new NpgsqlCommand(_freezeCommand, conn, tx);
        freezeCmd.Parameters.Add(new() { Value = queueName });
        freezeCmd.Parameters.Add(new() { Value = batchSize });
        await using (var reader = await freezeCmd.ExecuteReaderAsync())
        {
            while (true)
            {
                if (!reader.HasRows)
                {
                    break;
                }

                var hasRow = await reader.ReadAsync();
                if (!hasRow)
                {
                    break;
                }

                var frozenJob = new JobWithGroupModel
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    GroupId = reader.GetString(reader.GetOrdinal("serializable_group_id")),
                    JobName = reader.GetString(reader.GetOrdinal("job_name")),
                };
                frozenJobs.Add(frozenJob);
            }            
        }

        await tx.CommitAsync();
    }

    public async Task<GroupUnlockingStatusModel?> UnfreezeBatchAndUnlockIfAllUnfrozen()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        
        await using var tx = await conn.BeginTransactionAsync();
        
        await using var lockCmd = new NpgsqlCommand(_globalLockCommand, conn, tx);
        await lockCmd.ExecuteNonQueryAsync();

        GroupUnlockingStatusModel? unlockingStatus = null;
        await using var unlockCmd = new NpgsqlCommand(_unlockCommand, conn, tx);
        await using (var reader = await unlockCmd.ExecuteReaderAsync())
        {
            if (!reader.HasRows)
                return null;
        
            var hasRow = await reader.ReadAsync();
            if (!hasRow)
                return null;
        
            unlockingStatus = new GroupUnlockingStatusModel
            {
                GroupId = reader.GetString(reader.GetOrdinal("group_id")),
                IsUnlocked = reader.GetBoolean(reader.GetOrdinal("is_unlocked")),
            };    
        }
        
        await tx.CommitAsync();
        
        return unlockingStatus;
    }
}