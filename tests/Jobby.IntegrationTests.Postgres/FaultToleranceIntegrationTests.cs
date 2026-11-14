using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

// End-to-end fault tolerance: a server stops sending heartbeats ("dies"),
// another server detects the lost server, deletes its row and restarts
// its Processing jobs (only those with CanBeRestarted = true)
[Collection(PostgresqlTestsCollection.Name)]
public class FaultToleranceIntegrationTests
{
    [Fact]
    public async Task LostServer_RestartableJobsAreRestartedAndExecutedBySecondServer()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();

        // server1: fast heartbeat so it takes the jobs quickly, high lost-threshold
        // so it does not consider itself or others lost
        var server1 = CreateServer(executed, new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
            MaxNoHeartbeatIntervalSeconds = 300
        }, out var client);
        await server1.StartBackgroundServiceAsync();

        var restartableCommand = new TestJobCommand { DelayMs = 5000 };
        var restartableJobId = await client.EnqueueCommandAsync(restartableCommand, new JobOpts
        {
            CanBeRestartedIfServerGoesDown = true
        });

        // this one must never be restarted; its handler outlives the test
        var stuckCommand = new TestJobCommand { DelayMs = 20_000 };
        var stuckJobId = await client.EnqueueCommandAsync(stuckCommand, new JobOpts
        {
            CanBeRestartedIfServerGoesDown = false
        });

        await WaitUntilAsync(async () => await dbContext.Jobs.CountAsync(x =>
            (x.Id == restartableJobId || x.Id == stuckJobId) && x.Status == JobStatus.Processing,
            cancellationToken: TestContext.Current.CancellationToken) == 2, TimeSpan.FromSeconds(10));

        // exactly one server row must exist at this point (SingleAsync fails otherwise)
        var server1Id = await dbContext.Servers.AsNoTracking()
            .Select(x => x.Id)
            .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

        // server1 "dies": stops heartbeating while its handlers are still sleeping
        server1.SendStopSignal();

        // server2: detects lost servers quickly
        var server2 = CreateServer(executed, new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
            MaxNoHeartbeatIntervalSeconds = 2
        }, out _);
        await server2.StartBackgroundServiceAsync();
        try
        {
            // server1 row is deleted by server2
            var server1Deleted = await WaitUntilAsync(async () => !await dbContext.Servers.AsNoTracking()
                .AnyAsync(x => x.Id == server1Id, cancellationToken: TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(10));
            Assert.True(server1Deleted, "Lost server row was not deleted");

            // restartable job is re-executed by server2 and completes
            var restartCompleted = await WaitUntilAsync(async () =>
                executed.CountExecutions(restartableCommand.UniqueId) == 2 &&
                !await dbContext.Jobs.AsNoTracking()
                    .AnyAsync(x => x.Id == restartableJobId, cancellationToken: TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(20));
            Assert.True(restartCompleted, "Restartable job was not re-executed by server2");

            // non-restartable job stays Processing owned by the dead server and is never re-executed
            var stuckJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == stuckJobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Processing, stuckJob.Status);
            Assert.Equal(server1Id, stuckJob.ServerId);
            Assert.Equal(1, executed.CountExecutions(stuckCommand.UniqueId));

            // server2 keeps its own heartbeat row alive
            var server2Alive = await dbContext.Servers.AsNoTracking()
                .AnyAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(server2Alive);
        }
        finally
        {
            server2.SendStopSignal();
        }
    }

    private static IJobbyServer CreateServer(ExecutedCommandsList executed, JobbyServerSettings settings, out IJobbyClient client)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(executed));
        jobbyBuilder.UseServerSettings(settings);
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        client = jobbyBuilder.CreateJobbyClient();
        return jobbyBuilder.CreateJobbyServer();
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        return await condition();
    }
}
