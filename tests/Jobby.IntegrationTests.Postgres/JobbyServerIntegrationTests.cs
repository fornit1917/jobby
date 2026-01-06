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
    
    [Fact]
    public async Task CompleteWithBatchingOn_DeleteCompletedOn_ExecutesAndRemovesCommands()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var jobbyBuilder = ConfigureBuilder(new JobbyServerSettings
        {
            CompleteWithBatching = true,
            DeleteCompleted = true,
            PollingIntervalMs = 100
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
            jobsNotCompletedInDb = await dbContext.Jobs.AnyAsync(x => x.Id == jobId1 || x.Id == jobId2);
            if (jobsNotCompletedInDb)
            {
                await Task.Delay(50);
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
                .AnyAsync(x => (x.Id == jobId1 || x.Id == jobId2) && x.Status != JobStatus.Completed);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50);
            }
        }
        var actualJob1 = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId1);
        Assert.NotNull(actualJob1);
        Assert.Equal(JobStatus.Completed, actualJob1.Status);
        var actualJob2 = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId2);
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
            jobNotCompletedInDb = await dbContext.Jobs.AnyAsync(x => x.Id == jobId);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50);
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
            var jobNotCompletedInDb = await dbContext.Jobs.AnyAsync(x => x.Id == jobId && x.Status != JobStatus.Completed);
            if (jobNotCompletedInDb)
            {
                await Task.Delay(50);
            }
        }
        var actualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId);
        Assert.NotNull(actualJob);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.True(_executedCommands.HasCommandWithId(command.UniqueId));
        server.SendStopSignal();
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
