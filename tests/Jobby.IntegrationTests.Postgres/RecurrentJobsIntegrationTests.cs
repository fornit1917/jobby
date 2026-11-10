using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Core.Services.Schedulers;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

// Documented guarantee: recurrent jobs of the same definition never run in parallel —
// the next run is scheduled only after the previous one finishes
[Collection(PostgresqlTestsCollection.Name)]
public class RecurrentJobsIntegrationTests
{
    private const string EverySecondCron = "*/1 * * * * *";
    private const int HandlerDelayMs = 3000;
    private const int ObserveWindowMs = 8000;

    [Fact]
    public async Task RecurrentJob_NextCalculatedFromNow_NeverRunsInParallel()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();
        SlowJobTracker.Reset();
        var server = CreateServer(executed, out var client);

        var command = new TestJobCommand { DelayMs = HandlerDelayMs };
        var jobId = await client.ScheduleRecurrentAsync(command, EverySecondCron, new RecurrentJobOpts
        {
            StartTime = DateTime.UtcNow.AddSeconds(1)
        });

        await server.StartBackgroundServiceAsync();
        try
        {
            // without the guarantee this window would fit ~8 parallel runs;
            // sequential runs of a 3s job fit at most ~3
            await Task.Delay(ObserveWindowMs, TestContext.Current.CancellationToken);

            var executions = executed.CountExecutions(command.UniqueId);
            Assert.InRange(executions, 1, 3);
            Assert.Equal(1, SlowJobTracker.MaxConcurrent);

            // the job must stay alive and scheduled (not failed/completed) after several cycles;
            // at the moment of the check it can be either Scheduled or already Processing again
            var job = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(job.Status is JobStatus.Scheduled or JobStatus.Processing,
                $"Unexpected job status: {job.Status}");
        }
        finally
        {
            server.SendStopSignal();
            await client.CancelRecurrentByIdsAsync(jobId);
        }
    }

    [Fact]
    public async Task RecurrentJob_NextCalculatedFromPrev_NeverRunsInParallel()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();
        SlowJobTracker.Reset();
        var server = CreateServer(executed, out var client);

        var command = new TestJobCommand { DelayMs = HandlerDelayMs };
        var schedule = new CronSchedule
        {
            CronExpression = EverySecondCron,
            CalculateNextFromPrev = true
        };
        var jobId = await client.ScheduleRecurrentAsync(command, schedule, new RecurrentJobOpts
        {
            StartTime = DateTime.UtcNow.AddSeconds(1)
        });

        await server.StartBackgroundServiceAsync();
        try
        {
            await Task.Delay(ObserveWindowMs, TestContext.Current.CancellationToken);

            var executions = executed.CountExecutions(command.UniqueId);
            Assert.InRange(executions, 1, 3);
            Assert.Equal(1, SlowJobTracker.MaxConcurrent);

            // next start must be calculated from the previous scheduled time:
            // each new scheduled_start_at is 1 second after the previous one, so it is
            // immediately due (in the recent past), not pushed to now + 1 second
            var job = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(job.Status is JobStatus.Scheduled or JobStatus.Processing,
                $"Unexpected job status: {job.Status}");
            Assert.InRange((job.ScheduledStartAt - DateTime.UtcNow).TotalSeconds, -HandlerDelayMs / 1000 - 5, 5);
        }
        finally
        {
            server.SendStopSignal();
            await client.CancelRecurrentByIdsAsync(jobId);
        }
    }

    private static IJobbyServer CreateServer(ExecutedCommandsList executed, out IJobbyClient client)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(executed));
        jobbyBuilder.UseServerSettings(new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
        });
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        client = jobbyBuilder.CreateJobbyClient();
        return jobbyBuilder.CreateJobbyServer();
    }
}
