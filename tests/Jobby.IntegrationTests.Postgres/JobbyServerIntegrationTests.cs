using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

[Collection(PostgresqlTestsCollection.Name)]
public class JobbyServerIntegrationTests
{
    private readonly ExecutedCommandsList _executedCommands = new();
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompleteWithBatchingOn_DeleteCompletedOn_ExecutesAndRemovesCommands(bool disableSerializableGroups)
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = true,
            DeleteCompleted = true,
            PollingIntervalMs = 100,
            DisableSerializableGroups = disableSerializableGroups
        });
        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        server.StartBackgroundService();
        var command1 = new TestJobCommand();
        var command2 = new TestJobCommand();
        var jobId1 = await client.EnqueueCommandAsync(command1);
        var jobId2 = await client.EnqueueCommandAsync(command2);

        var jobsNotCompletedInDb = true;
        for (int i = 0; i < 50; i++)
        {
            jobsNotCompletedInDb = await dbContext.Jobs
                .AnyAsync(x => x.Id == jobId1 || x.Id == jobId2,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (jobsNotCompletedInDb)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        Assert.False(jobsNotCompletedInDb);
        Assert.True(_executedCommands.HasCommandWithId(command1.UniqueId));
        Assert.True(_executedCommands.HasCommandWithId(command2.UniqueId));
        server.SendStopSignal();
    }
    
    [Fact]
    public async Task MQ_CompleteWithBatchingOn_DeleteCompletedOn_ExecutesAndRemovesCommands()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = true,
            DeleteCompleted = true,
            PollingIntervalMs = 100,
            Queues =
            [
                new QueueSettings { QueueName = "q1" },
                new QueueSettings { QueueName = "q2" }
            ]
        });
        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        server.StartBackgroundService();
        var command1 = new TestJobCommand();
        var command2 = new TestJobCommand();
        
        var jobId1 = await client.EnqueueCommandAsync(command1, new JobOpts { QueueName = "q1" });
        var jobId2 = await client.EnqueueCommandAsync(command2, new JobOpts { QueueName = "q2" });

        var jobsNotCompletedInDb = true;
        for (int i = 0; i < 50; i++)
        {
            jobsNotCompletedInDb = await dbContext.Jobs
                .AnyAsync(x => x.Id == jobId1 || x.Id == jobId2,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (jobsNotCompletedInDb)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        Assert.False(jobsNotCompletedInDb);
        Assert.True(_executedCommands.HasCommandWithId(command1.UniqueId));
        Assert.True(_executedCommands.HasCommandWithId(command2.UniqueId));
        server.SendStopSignal();
    }    

    [Fact]
    public async Task CompleteWithBatchingOn_DeleteCompletedOff_ExecutesAndRemovesCommands()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = true,
            DeleteCompleted = false,
            PollingIntervalMs = 100
        });
        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        server.StartBackgroundService();
        var command1 = new TestJobCommand();
        var command2 = new TestJobCommand();
        var jobId1 = await client.EnqueueCommandAsync(command1);
        var jobId2 = await client.EnqueueCommandAsync(command2);

        for (int i = 0; i < 50; i++)
        {
            var jobNotCompletedInDb = await dbContext.Jobs
                .AnyAsync(x => (x.Id == jobId1 || x.Id == jobId2) && x.Status != JobStatus.Completed,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        var actualJob1 = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob1);
        Assert.Equal(JobStatus.Completed, actualJob1.Status);
        var actualJob2 = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob2);
        Assert.Equal(JobStatus.Completed, actualJob1.Status);
        Assert.True(_executedCommands.HasCommandWithId(command1.UniqueId));
        Assert.True(_executedCommands.HasCommandWithId(command2.UniqueId));
        server.SendStopSignal();
    }

    [Fact]
    public async Task CompleteWithBatchingOff_DeleteCompletedOn_ExecutesAndRemovesCommand()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = false,
            DeleteCompleted = true,
            PollingIntervalMs = 100
        });
        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();

        server.StartBackgroundService();
        var command = new TestJobCommand();
        var jobId = await client.EnqueueCommandAsync(command);

        var jobNotCompletedInDb = true;
        for (int i = 0; i < 50; i++)
        {
            jobNotCompletedInDb = await dbContext.Jobs
                .AnyAsync(x => x.Id == jobId,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        Assert.False(jobNotCompletedInDb);
        Assert.True(_executedCommands.HasCommandWithId(command.UniqueId));
        server.SendStopSignal();
    }

    [Fact]
    public async Task CompleteWithBatchingOff_DeleteCompletedOff_ExecutesAndRemovesCommand()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = false,
            DeleteCompleted = false,
            PollingIntervalMs = 100
        });
        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();
        
        server.StartBackgroundService();
        var command = new TestJobCommand();
        var jobId = await client.EnqueueCommandAsync(command);

        for (int i = 0; i < 50; i++)
        {
            var jobNotCompletedInDb = await dbContext.Jobs
                .AnyAsync(x => x.Id == jobId && x.Status != JobStatus.Completed,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.True(_executedCommands.HasCommandWithId(command.UniqueId));
        server.SendStopSignal();
    }

    [Fact]
    public async Task FreezesPermanentlyLockedGroupAndThenUnlocksByRequest()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            PollingIntervalMs = 100,
            PermanentLockedFreezingIntervalSeconds = 1,
            PermanentLockedHandleUnlockingRequestsIntervalSeconds = 1,
        });

        var client = jobbyBuilder.CreateJobbyClient();
        var server = jobbyBuilder.CreateJobbyServer();
        server.StartBackgroundService();

        var groupId = Guid.NewGuid().ToString();
        var failedCommand = new TestJobCommand
        {
            ExceptionToThrow = new Exception("test exception"),
        };
        var failedJobId = await client.EnqueueCommandAsync(failedCommand, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
        });
        
        var nextJobCommand = new TestJobCommand();
        var nextJobId = await  client.EnqueueCommandAsync(nextJobCommand, new JobOpts
        {
            SerializableGroupId = groupId,
            LockGroupIfFailed = true,
            StartTime = DateTime.UtcNow.AddMinutes(-1),
        });

        bool isNextJobFrozen = false;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            isNextJobFrozen = await dbContext.Jobs
                .AnyAsync(x => x.Id == nextJobId && x.Status == JobStatus.Frozen,
                    cancellationToken: TestContext.Current.CancellationToken);
            if (isNextJobFrozen)
                break;
        }
        
        Assert.True(isNextJobFrozen);
        Assert.False(_executedCommands.HasCommandWithId(nextJobCommand.UniqueId));

        var unlockingRequest = new UnlockingGroupDbModel
        {
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.UnlockingGroups.Add(unlockingRequest);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var isNextJobExecuted = false;
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1000, TestContext.Current.CancellationToken);
            isNextJobExecuted = _executedCommands.HasCommandWithId(nextJobCommand.UniqueId);
            if (isNextJobExecuted)
                break;
        }
        
        server.SendStopSignal();
        
        Assert.True(isNextJobExecuted);
        var actualFailedJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == failedJobId);
        Assert.Null(actualFailedJob);
        var actualUnlockingRequest = dbContext.UnlockingGroups.AsNoTracking().FirstOrDefault(x => x.GroupId == groupId);
        Assert.Null(actualUnlockingRequest);
    }

    private JobbyBuilder ConfigureBuilder(JobbyServerSettings serverSettings)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        jobbyBuilder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(_executedCommands));
        jobbyBuilder.UseServerSettings(serverSettings);
        jobbyBuilder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();
        return jobbyBuilder;
    }
}
