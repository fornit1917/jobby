using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

// Documented guarantees for job sequences:
// 1. jobs of a sequence run strictly in order, even when placed in different queues;
// 2. a sequence stops on the first failed job — subsequent jobs are never executed
[Collection(PostgresqlTestsCollection.Name)]
public class SequencesIntegrationTests
{
    [Fact]
    public async Task Sequence_AcrossDifferentQueues_ExecutesStrictlyInOrder()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();
        var (server, client) = CreateServer(executed, new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
            Queues =
            [
                new QueueSettings { QueueName = "q1" },
                new QueueSettings { QueueName = "q2" }
            ]
        });

        var command1 = new TestJobCommand();
        var command2 = new TestJobCommand();
        var command3 = new TestJobCommand();

        var jobs = client.Factory.CreateSequenceBuilder()
            .Add(command1, new JobOpts { QueueName = "q2" })
            .Add(command2, new JobOpts { QueueName = "q1" })
            .Add(command3, new JobOpts { QueueName = "q2" })
            .GetJobs();

        await client.EnqueueBatchAsync(jobs);
        await server.StartBackgroundServiceAsync();
        try
        {
            var allExecuted = await WaitUntilAsync(
                () => Task.FromResult(executed.CountExecutions(command3.UniqueId) == 1),
                TimeSpan.FromSeconds(15));
            Assert.True(allExecuted, "Sequence did not finish");

            var order = executed.GetUniqueIdsInExecutionOrder();
            Assert.Equal(new[] { command1.UniqueId, command2.UniqueId, command3.UniqueId }, order);

            // DeleteCompleted = true: all three jobs are removed after completion,
            // including jobs that started in WaitingPrev status
            var jobsLeft = await dbContext.Jobs.AsNoTracking()
                .CountAsync(x => x.Id == jobs[0].Id || x.Id == jobs[1].Id || x.Id == jobs[2].Id,
                    cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(0, jobsLeft);
        }
        finally
        {
            server.SendStopSignal();
        }
    }

    [Fact]
    public async Task Sequence_MiddleJobFailsWithoutRetry_NextJobNeverExecutes()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var executed = new ExecutedCommandsList();
        var (server, client) = CreateServer(executed, new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            HeartbeatIntervalSeconds = 1,
        });

        var command1 = new TestJobCommand();
        var failingCommand = new TestJobCommand
        {
            ExceptionToThrow = new Exception("middle job failed")
        };
        var command3 = new TestJobCommand();

        var jobs = client.Factory.CreateSequenceBuilder()
            .Add(command1)
            .Add(failingCommand)
            .Add(command3)
            .GetJobs();

        await client.EnqueueBatchAsync(jobs);
        await server.StartBackgroundServiceAsync();
        try
        {
            // middle job must fail permanently (default NoRetry policy)
            var failed = await WaitUntilAsync(async () => await dbContext.Jobs.AsNoTracking()
                .AnyAsync(x => x.Id == jobs[1].Id && x.Status == JobStatus.Failed,
                    cancellationToken: TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(15));
            Assert.True(failed, "Middle job did not reach Failed status");

            // give the server time to (wrongly) execute the third job if the guarantee is broken
            await Task.Delay(2000, TestContext.Current.CancellationToken);

            Assert.Equal(0, executed.CountExecutions(command3.UniqueId));
            var thirdJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobs[2].Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.WaitingPrev, thirdJob.Status);

            var middleJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobs[1].Id, cancellationToken: TestContext.Current.CancellationToken);
            // the exact exception message does not survive the job_param JSON roundtrip,
            // so only check that the error is stored
            Assert.False(string.IsNullOrEmpty(middleJob.Error), "Failed job must store the error");
        }
        finally
        {
            server.SendStopSignal();
        }
    }

    private static (IJobbyServer Server, IJobbyClient Client) CreateServer(ExecutedCommandsList executed, JobbyServerSettings settings)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(executed));
        jobbyBuilder.UseServerSettings(settings);
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        return (jobbyBuilder.CreateJobbyServer(), jobbyBuilder.CreateJobbyClient());
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
