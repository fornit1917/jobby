using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

// Documented guarantee: a failing job is re-executed according to its RetryPolicy
// in a live server loop, then marked Failed with the error stored
[Collection(PostgresqlTestsCollection.Name)]
public class RetryIntegrationTests
{
    [Fact]
    public async Task JobWithRetryPolicy_ExecutesMaxCountTimes_ThenFailsWithStoredError()
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
        });
        jobbyBuilder.UseRetryPolicyForJob<TestJobCommand>(new RetryPolicy
        {
            MaxCount = 2,
            IntervalsSeconds = [1]
        });
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        var command = new TestJobCommand
        {
            ExceptionToThrow = new Exception("job failed on purpose")
        };
        var jobId = await client.EnqueueCommandAsync(command);

        await server.StartBackgroundServiceAsync();
        try
        {
            // first run fails, retry after 1 second fails again, MaxCount = 2 -> Failed
            var finished = await WaitUntilAsync(async () => await dbContext.Jobs.AsNoTracking()
                .AnyAsync(x => x.Id == jobId && x.Status == JobStatus.Failed,
                    cancellationToken: TestContext.Current.CancellationToken),
                TimeSpan.FromSeconds(15));
            Assert.True(finished, "Job did not reach Failed status");

            var job = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == jobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(2, job.StartedCount);
            // the exact exception message does not survive the job_param JSON roundtrip
            // (System.Text.Json cannot deserialize Exception.Message), so only check
            // that the error of the last attempt is stored
            Assert.False(string.IsNullOrEmpty(job.Error), "Failed job must store the error");
            Assert.Equal(2, executed.CountExecutions(command.UniqueId));
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
