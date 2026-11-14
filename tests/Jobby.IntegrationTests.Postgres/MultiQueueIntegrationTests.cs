using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

[Collection(PostgresqlTestsCollection.Name)]
public class MultiQueueIntegrationTests
{
    [Fact]
    public async Task JobInQueueNotSpecifiedToServer_IsNotExecuted()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();

        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(executed));
        jobbyBuilder.UseServerSettings(new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
            Queues =
            [
                new QueueSettings { QueueName = "q1" }
            ]
        });
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        var command = new TestJobCommand();
        var jobId = await client.EnqueueCommandAsync(command, new JobOpts { QueueName = "q2" });

        await server.StartBackgroundServiceAsync();
        try
        {
            // documented guarantee: jobs from queues not listed in server settings are not executed
            await Task.Delay(3000, TestContext.Current.CancellationToken);

            Assert.Equal(0, executed.CountExecutions(command.UniqueId));
            var job = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Scheduled, job.Status);
            Assert.Equal("q2", job.QueueName);
        }
        finally
        {
            server.SendStopSignal();
        }
    }

    [Fact(Skip = "Known broken: per-queue MaxDegreeOfParallelism is not enforced by the server yet")]
    public async Task PerQueueMaxDegreeOfParallelism_LimitsConcurrencyWithinQueue()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();
        SlowJobTracker.Reset();

        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(executed));
        jobbyBuilder.UseServerSettings(new JobbyServerSettings
        {
            PollingIntervalMs = 50,
            HeartbeatIntervalSeconds = 1,
            Queues =
            [
                new QueueSettings { QueueName = "heavy", MaxDegreeOfParallelism = 1 }
            ]
        });
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        var commands = Enumerable.Range(0, 4)
            .Select(_ => new TestJobCommand { DelayMs = 500 })
            .ToList();

        await server.StartBackgroundServiceAsync();
        try
        {
            foreach (var command in commands)
            {
                await client.EnqueueCommandAsync(command, new JobOpts { QueueName = "heavy" });
            }

            var allExecuted = await WaitUntilAsync(
                () => Task.FromResult(commands.All(c => executed.CountExecutions(c.UniqueId) == 1)),
                TimeSpan.FromSeconds(15));

            Assert.True(allExecuted, "Not all jobs were executed");
            Assert.Equal(1, SlowJobTracker.MaxConcurrent);
        }
        finally
        {
            server.SendStopSignal();
        }
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
