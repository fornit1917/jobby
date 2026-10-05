using System.Text.Json;
using Jobby.Core.Models;
using Jobby.Postgres.Dashboard;
using Jobby.Samples.AspNet.Jobs;
using Npgsql;

namespace Jobby.Samples.AspNet.DashboardDemo;

public sealed class DashboardDemoSeeder
{
    private static readonly Guid ChainWaitingJobId = Guid.Parse("10000000-0000-0000-0000-000000000007");

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DashboardDemoSeeder> _logger;
    private readonly string _jobsTable;
    private readonly string _serversTable;

    public DashboardDemoSeeder(
        NpgsqlDataSource dataSource,
        PostgresqlDashboardStorageOptions storageOptions,
        ILogger<DashboardDemoSeeder> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        _jobsTable = TableName(storageOptions, "jobs");
        _serversTable = TableName(storageOptions, "servers");
    }

    private static string TableName(PostgresqlDashboardStorageOptions options, string name)
    {
        return string.IsNullOrEmpty(options.SchemaName)
            ? $"\"{options.TablesPrefix}{name}\""
            : $"\"{options.SchemaName}\".\"{options.TablesPrefix}{name}\"";
    }

    public async Task<DashboardDemoSeedResult> SeedAsync(bool reset = true,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var jobs = CreateJobs(now);
        var servers = CreateServers(now);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var batch = new NpgsqlBatch(conn);

        if (reset)
        {
            batch.BatchCommands.Add(new NpgsqlBatchCommand($"""
                                                            DELETE FROM {_jobsTable}
                                                            WHERE id IN (
                                                                '10000000-0000-0000-0000-000000000001',
                                                                '10000000-0000-0000-0000-000000000002',
                                                                '10000000-0000-0000-0000-000000000003',
                                                                '10000000-0000-0000-0000-000000000004',
                                                                '10000000-0000-0000-0000-000000000005',
                                                                '10000000-0000-0000-0000-000000000006',
                                                                '10000000-0000-0000-0000-000000000007',
                                                                '10000000-0000-0000-0000-000000000008',
                                                                '10000000-0000-0000-0000-000000000009',
                                                                '10000000-0000-0000-0000-000000000010',
                                                                '10000000-0000-0000-0000-000000000011',
                                                                '10000000-0000-0000-0000-000000000012',
                                                                '10000000-0000-0000-0000-000000000013',
                                                                '10000000-0000-0000-0000-000000000014'
                                                            )
                                                            OR job_name LIKE 'DashboardDemo:%'
                                                            OR job_param LIKE '%dashboard-demo:%';
                                                            """));

            batch.BatchCommands.Add(new NpgsqlBatchCommand($"""
                                                            DELETE FROM {_serversTable}
                                                            WHERE id IN ('dashboard-demo-live-a', 'dashboard-demo-live-b', 'dashboard-demo-stale',
                                                                         'dashboard-demo-lagging');
                                                            """));
        }

        foreach (var job in jobs)
        {
            batch.BatchCommands.Add(CreateJobCommand(job));
        }

        foreach (var server in servers)
        {
            batch.BatchCommands.Add(CreateServerCommand(server));
        }

        await batch.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Seeded dashboard demo data: {JobsCount} jobs, {ServersCount} servers",
            jobs.Count, servers.Count);

        return new DashboardDemoSeedResult(jobs.Count, servers.Count);
    }

    private static IReadOnlyList<DemoJobRow> CreateJobs(DateTime now)
    {
        return
        [
            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000001"),
                JobName: DemoJobCommand.GetJobName(),
                JobParam: DemoParam("dashboard-demo: scheduled default queue"),
                Status: JobStatus.Scheduled,
                QueueName: QueueSettings.DefaultQueueName,
                CreatedAt: now.AddMinutes(-70),
                ScheduledStartAt: now.AddHours(2),
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000002"),
                JobName: DemoJobCommand.GetJobName(),
                JobParam: DemoParam("dashboard-demo: scheduled email queue", delayMs: 250),
                Status: JobStatus.Scheduled,
                QueueName: "emails",
                CreatedAt: now.AddMinutes(-55),
                ScheduledStartAt: now.AddHours(6),
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000003"),
                JobName: "DashboardDemo:EmailDigestJob",
                JobParam: JsonParam("dashboard-demo: email digest completed"),
                Status: JobStatus.Completed,
                QueueName: "emails",
                CreatedAt: now.AddHours(-4),
                ScheduledStartAt: now.AddHours(-3.5),
                LastStartedAt: now.AddHours(-3.5),
                LastFinishedAt: now.AddHours(-3.45),
                StartedCount: 1,
                ServerId: "dashboard-demo-live-a",
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000004"),
                JobName: "DashboardDemo:VideoTranscodeJob",
                JobParam: JsonParam("dashboard-demo: long running media job"),
                Status: JobStatus.Processing,
                QueueName: "media",
                CreatedAt: now.AddMinutes(-25),
                ScheduledStartAt: now.AddMinutes(-20),
                LastStartedAt: now.AddMinutes(-12),
                StartedCount: 1,
                ServerId: "dashboard-demo-live-b",
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000005"),
                JobName: DemoJobCommand.GetJobName(),
                JobParam: DemoParam("dashboard-demo: failed report export", shouldFail: true),
                Status: JobStatus.Failed,
                QueueName: "reports",
                CreatedAt: now.AddHours(-2),
                ScheduledStartAt: now.AddHours(-1.8),
                LastStartedAt: now.AddHours(-1.7),
                LastFinishedAt: now.AddHours(-1.68),
                StartedCount: 3,
                ServerId: "dashboard-demo-live-a",
                Error: "System.InvalidOperationException: Report export timed out after 30 seconds.",
                SerializableGroupId: "dashboard-demo-customer-42",
                LockGroupIfFailed: true,
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000006"),
                JobName: "DashboardDemo:PaymentCaptureJob",
                JobParam: JsonParam("dashboard-demo: frozen behind failed group locker"),
                Status: JobStatus.Frozen,
                QueueName: "billing",
                CreatedAt: now.AddHours(-1.5),
                ScheduledStartAt: now.AddHours(-1.4),
                SerializableGroupId: "dashboard-demo-customer-42",
                CanBeRestarted: true),

            new(
                Id: ChainWaitingJobId,
                JobName: "DashboardDemo:InvoiceFollowUpJob",
                JobParam: JsonParam("dashboard-demo: waiting for previous sequence job"),
                Status: JobStatus.WaitingPrev,
                QueueName: QueueSettings.DefaultQueueName,
                CreatedAt: now.AddMinutes(-40),
                ScheduledStartAt: now.AddMinutes(-35),
                SerializableGroupId: "dashboard-demo-sequence-1",
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000008"),
                JobName: "DashboardDemo:InvoiceRootJob",
                JobParam: JsonParam("dashboard-demo: completed root with next job"),
                Status: JobStatus.Completed,
                QueueName: QueueSettings.DefaultQueueName,
                CreatedAt: now.AddMinutes(-50),
                ScheduledStartAt: now.AddMinutes(-45),
                LastStartedAt: now.AddMinutes(-45),
                LastFinishedAt: now.AddMinutes(-44),
                StartedCount: 1,
                NextJobId: ChainWaitingJobId,
                ServerId: "dashboard-demo-live-a",
                SerializableGroupId: "dashboard-demo-sequence-1",
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000009"),
                JobName: "DashboardDemo:ArchivedCleanupJob",
                JobParam: JsonParam("dashboard-demo: scheduled non-restartable cleanup"),
                Status: JobStatus.Scheduled,
                QueueName: "maintenance",
                CreatedAt: now.AddMinutes(-15),
                ScheduledStartAt: now.AddDays(3),
                CanBeRestarted: false),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000010"),
                JobName: DemoJobCommand.GetJobName(),
                JobParam: DemoParam("dashboard-demo: ignored exception example", ignoredException: true),
                Status: JobStatus.Failed,
                QueueName: QueueSettings.DefaultQueueName,
                CreatedAt: now.AddMinutes(-95),
                ScheduledStartAt: now.AddMinutes(-90),
                LastStartedAt: now.AddMinutes(-90),
                LastFinishedAt: now.AddMinutes(-89),
                StartedCount: 1,
                ServerId: "dashboard-demo-live-b",
                Error: "ExceptionShouldBeIgnored: This row demonstrates failed-state details.",
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000011"),
                JobName: "DashboardDemo:LongRunningImportJob",
                JobParam: JsonParam("dashboard-demo: processing on stale server"),
                Status: JobStatus.Processing,
                QueueName: "imports",
                CreatedAt: now.AddMinutes(-120),
                ScheduledStartAt: now.AddMinutes(-115),
                LastStartedAt: now.AddMinutes(-110),
                StartedCount: 2,
                ServerId: "dashboard-demo-lagging",
                CanBeRestarted: false),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000012"),
                JobName: "DashboardDemo:DailyDigestRecurrent",
                JobParam: JsonParam("dashboard-demo: daily digest recurrent template"),
                Status: JobStatus.Scheduled,
                QueueName: "recurrent",
                CreatedAt: now.AddDays(-2),
                ScheduledStartAt: now.AddHours(8),
                Schedule: "0 0 9 * * *",
                SchedulerType: "JOBBY_CRON",
                IsExclusive: true,
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000013"),
                JobName: "DashboardDemo:FiveMinuteSyncRecurrent",
                JobParam: JsonParam("dashboard-demo: five minute recurrent template"),
                Status: JobStatus.Scheduled,
                QueueName: "recurrent",
                CreatedAt: now.AddDays(-1),
                ScheduledStartAt: now.AddMinutes(30),
                LastStartedAt: now.AddMinutes(-5),
                LastFinishedAt: now.AddMinutes(-4),
                Schedule: "*/5 * * * * *",
                SchedulerType: "JOBBY_CRON",
                IsExclusive: true,
                CanBeRestarted: true),

            new(
                Id: Guid.Parse("10000000-0000-0000-0000-000000000014"),
                JobName: "DashboardDemo:CustomSchedulerRecurrent",
                JobParam: JsonParam("dashboard-demo: custom scheduler recurrent template"),
                Status: JobStatus.Scheduled,
                QueueName: "recurrent",
                CreatedAt: now.AddHours(-6),
                ScheduledStartAt: now.AddHours(1),
                Schedule: """{"SecondsInterval":45}""",
                SchedulerType: "CUSTOM_SECONDS_INTERVAL",
                IsExclusive: true,
                CanBeRestarted: true),
        ];
    }

    private static IReadOnlyList<DemoServerRow> CreateServers(DateTime now)
    {
        return
        [
            new("dashboard-demo-live-a", now.AddSeconds(-8)),
            new("dashboard-demo-live-b", now.AddSeconds(-20)),
            new("dashboard-demo-lagging", now.AddMinutes(-4)),
        ];
    }

    private NpgsqlBatchCommand CreateJobCommand(DemoJobRow job)
    {
        return new NpgsqlBatchCommand($"""
                                       INSERT INTO {_jobsTable} (
                                           id, job_name, job_param, status, error, created_at, scheduled_start_at,
                                           last_started_at, last_finished_at, started_count, next_job_id, server_id,
                                           can_be_restarted, queue_name, serializable_group_id, lock_group_if_failed,
                                           is_exclusive, schedule, scheduler_type
                                       )
                                       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)
                                       ON CONFLICT (id) DO UPDATE SET
                                           job_name = EXCLUDED.job_name,
                                           job_param = EXCLUDED.job_param,
                                           status = EXCLUDED.status,
                                           error = EXCLUDED.error,
                                           created_at = EXCLUDED.created_at,
                                           scheduled_start_at = EXCLUDED.scheduled_start_at,
                                           last_started_at = EXCLUDED.last_started_at,
                                           last_finished_at = EXCLUDED.last_finished_at,
                                           started_count = EXCLUDED.started_count,
                                           next_job_id = EXCLUDED.next_job_id,
                                           server_id = EXCLUDED.server_id,
                                           can_be_restarted = EXCLUDED.can_be_restarted,
                                           queue_name = EXCLUDED.queue_name,
                                           serializable_group_id = EXCLUDED.serializable_group_id,
                                           lock_group_if_failed = EXCLUDED.lock_group_if_failed,
                                           is_exclusive = EXCLUDED.is_exclusive,
                                           schedule = EXCLUDED.schedule,
                                           scheduler_type = EXCLUDED.scheduler_type;
                                       """)
        {
            Parameters =
            {
                new() { Value = job.Id },
                new() { Value = job.JobName },
                new() { Value = (object?)job.JobParam ?? DBNull.Value },
                new() { Value = (int)job.Status },
                new() { Value = (object?)job.Error ?? DBNull.Value },
                new() { Value = job.CreatedAt },
                new() { Value = job.ScheduledStartAt },
                new() { Value = (object?)job.LastStartedAt ?? DBNull.Value },
                new() { Value = (object?)job.LastFinishedAt ?? DBNull.Value },
                new() { Value = job.StartedCount },
                new() { Value = (object?)job.NextJobId ?? DBNull.Value },
                new() { Value = (object?)job.ServerId ?? DBNull.Value },
                new() { Value = job.CanBeRestarted },
                new() { Value = job.QueueName },
                new() { Value = (object?)job.SerializableGroupId ?? DBNull.Value },
                new() { Value = job.LockGroupIfFailed },
                new() { Value = job.IsExclusive },
                new() { Value = (object?)job.Schedule ?? DBNull.Value },
                new() { Value = (object?)job.SchedulerType ?? DBNull.Value },
            }
        };
    }

    private NpgsqlBatchCommand CreateServerCommand(DemoServerRow server)
    {
        return new NpgsqlBatchCommand($"""
                                       INSERT INTO {_serversTable} (id, heartbeat_ts)
                                       VALUES ($1, $2)
                                       ON CONFLICT (id) DO UPDATE SET heartbeat_ts = EXCLUDED.heartbeat_ts;
                                       """)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = server.Id },
                new NpgsqlParameter { Value = server.HeartbeatTs },
            }
        };
    }

    private static string DemoParam(
        string value,
        bool shouldFail = false,
        bool ignoredException = false,
        int delayMs = 0)
    {
        return JsonSerializer.Serialize(new DemoJobCommand
        {
            Value = value,
            ShouldBeFailed = shouldFail,
            ShouldThrowIgnoredException = ignoredException,
            DelayMs = delayMs,
        });
    }

    private static string JsonParam(string label)
    {
        return JsonSerializer.Serialize(new
        {
            Demo = true,
            Label = label,
            SeededBy = nameof(DashboardDemoSeeder),
        });
    }

    private sealed record DemoServerRow(string Id, DateTime HeartbeatTs);

    private sealed record DemoJobRow(
        Guid Id,
        string JobName,
        string? JobParam,
        JobStatus Status,
        string QueueName,
        DateTime CreatedAt,
        DateTime ScheduledStartAt,
        DateTime? LastStartedAt = null,
        DateTime? LastFinishedAt = null,
        int StartedCount = 0,
        Guid? NextJobId = null,
        string? ServerId = null,
        string? Error = null,
        string? SerializableGroupId = null,
        bool LockGroupIfFailed = false,
        bool IsExclusive = false,
        string? Schedule = null,
        string? SchedulerType = null,
        bool CanBeRestarted = true);
}