using Jobby.Core.Models;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

// Full freeze/unfreeze cycle through a live server:
// a permanently locked group freezes all queued jobs of the group, then an
// unlocking request unfreezes them in scheduled_start_at order, the failed
// locker job and the request row are removed, and the group runs again
[Collection(PostgresqlTestsCollection.Name)]
public class FreezeUnfreezeIntegrationTests
{
    [Fact]
    public async Task PermanentlyLockedGroup_FreezeAllQueuedJobs_UnfreezeByRequestInOrder()
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
            PermanentLockedFreezingIntervalSeconds = 1,
            PermanentLockedHandleUnlockingRequestsIntervalSeconds = 1,
        });
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();

        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();
        await server.StartBackgroundServiceAsync();

        var groupId = Guid.NewGuid().ToString();

        // the locker: fails permanently and locks the group
        var lockerCommand = new TestJobCommand
        {
            ExceptionToThrow = new Exception("locker job failed")
        };
        var lockerJobId = await client.EnqueueCommandAsync(lockerCommand, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
        });

        // queued group jobs with staggered start times
        var command1 = new TestJobCommand();
        var command2 = new TestJobCommand();
        var command3 = new TestJobCommand();
        var jobId1 = await client.EnqueueCommandAsync(command1, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-4),
        });
        var jobId2 = await client.EnqueueCommandAsync(command2, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-3),
        });
        var jobId3 = await client.EnqueueCommandAsync(command3, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
        });

        try
        {
            // all queued jobs of the group are frozen after the locker fails forever
            var allFrozen = await WaitUntilAsync(async () => await dbContext.Jobs
                .CountAsync(x => (x.Id == jobId1 || x.Id == jobId2 || x.Id == jobId3)
                                && x.Status == JobStatus.Frozen,
                    cancellationToken: TestContext.Current.CancellationToken) == 3,
                TimeSpan.FromSeconds(10));
            Assert.True(allFrozen, "Not all group jobs were frozen");
            Assert.Equal(0, executed.CountExecutions(command1.UniqueId));

            // request the unlock
            dbContext.UnlockingGroups.Add(new UnlockingGroupDbModel
            {
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // all frozen jobs are unfrozen and executed in scheduled_start_at order
            var allExecuted = await WaitUntilAsync(
                () => Task.FromResult(executed.CountExecutions(command3.UniqueId) == 1),
                TimeSpan.FromSeconds(20));
            Assert.True(allExecuted, "Frozen jobs were not executed after unlock");

            // execution order list also contains the failed locker run, so compare
            // the relative order of the unfrozen jobs instead of the exact sequence
            var order = executed.GetUniqueIdsInExecutionOrder().ToList();
            var index1 = order.IndexOf(command1.UniqueId);
            var index2 = order.IndexOf(command2.UniqueId);
            var index3 = order.IndexOf(command3.UniqueId);
            Assert.NotEqual(-1, index1);
            Assert.NotEqual(-1, index2);
            Assert.NotEqual(-1, index3);
            Assert.True(index1 < index2 && index2 < index3,
                $"Unfrozen jobs executed out of scheduled_start_at order: [{string.Join(", ", order)}]");

            // the failed locker job and the unlocking request are removed
            var lockerLeft = await dbContext.Jobs.AsNoTracking()
                .AnyAsync(x => x.Id == lockerJobId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(lockerLeft, "Failed locker job was not deleted");
            var requestLeft = await dbContext.UnlockingGroups.AsNoTracking()
                .AnyAsync(x => x.GroupId == groupId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(requestLeft, "Unlocking request was not deleted");
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
