using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

[Collection(PostgresqlTestsCollection.Name)]
public class JobbyClientIntegrationTests
{
    [Fact]
    public async Task EnqueuesAndCancelsCommandByAsyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var jobId = await client.EnqueueCommandAsync(command, DateTime.UtcNow.AddDays(1));

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);

        await client.CancelJobsByIdsAsync(jobId);
        actualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId);
        Assert.Null(actualJob);
    }

    [Fact]
    public void EnqueuesAndCancelsCommandBySyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var jobId = client.EnqueueCommand(command, DateTime.UtcNow.AddDays(1));

        var actualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == jobId);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);

        client.CancelJobsByIds(jobId);
        actualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == jobId);
        Assert.Null(actualJob);
    }

    [Fact]
    public async Task EnqueuesAndCancelsBatchByAsyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();
        var jobsFactory = CreateJobsFactory();

        var jobs = new List<JobCreationModel>
        {
            jobsFactory.Create(new TestJobCommand(), DateTime.UtcNow.AddDays(1)),
            jobsFactory.Create(new TestJobCommand(), DateTime.UtcNow.AddDays(2)),
        };

        await client.EnqueueBatchAsync(jobs);

        var ids = jobs.Select(x => x.Id).ToArray();
        var actualJobsFromDb = await dbContext.Jobs.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();
        Assert.Equal(2, actualJobsFromDb.Count);

        await client.CancelJobsByIdsAsync(ids);
        actualJobsFromDb = await dbContext.Jobs.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();
        Assert.Empty(actualJobsFromDb);
    }

    [Fact]
    public void EnqueuesAndCancelsBatchBySyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();
        var jobsFactory = CreateJobsFactory();

        var jobs = new List<JobCreationModel>
        {
            jobsFactory.Create(new TestJobCommand(), DateTime.UtcNow.AddDays(1)),
            jobsFactory.Create(new TestJobCommand(), DateTime.UtcNow.AddDays(2)),
        };

        client.EnqueueBatch(jobs);

        var ids = jobs.Select(x => x.Id).ToArray();
        var actualJobsFromDb = dbContext.Jobs.AsNoTracking().Where(x => ids.Contains(x.Id)).ToList();
        Assert.Equal(2, actualJobsFromDb.Count);

        client.CancelJobsByIds(ids);
        actualJobsFromDb = dbContext.Jobs.AsNoTracking().Where(x => ids.Contains(x.Id)).ToList();
        Assert.Empty(actualJobsFromDb);
    }

    [Fact]
    public async Task SchedulesRecurrentAndCancelsByAsyncMethods()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        await client.ScheduleRecurrentAsync(command, cron);

        var actualJobFromDb = await dbContext.Jobs.AsNoTracking()
                                             .Where(x => x.JobName == TestJobCommand.GetJobName() && x.Cron == cron)
                                             .FirstOrDefaultAsync();
        Assert.NotNull(actualJobFromDb);

        await client.CancelRecurrentAsync<TestJobCommand>();

        actualJobFromDb = await dbContext.Jobs.AsNoTracking()
                                             .Where(x => x.JobName == TestJobCommand.GetJobName())
                                             .FirstOrDefaultAsync();
        Assert.Null(actualJobFromDb);
    }

    [Fact]
    public void SchedulesRecurrentAndCancelsBySyncMethods()
    {
        var dbContext = DbHelper.CreateContextAndClearDb();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        client.ScheduleRecurrent(command, cron);

        var actualJobFromDb = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName() && x.Cron == cron);
        Assert.NotNull(actualJobFromDb);

        client.CancelRecurrent<TestJobCommand>();

        actualJobFromDb = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName());
        Assert.Null(actualJobFromDb);
    }

    private IJobbyClient CreateJobbyClient()
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.UsePostgresql(DbHelper.DataSource);
        return jobbyBuilder.CreateJobbyClient();
    }

    private IJobsFactory CreateJobsFactory()
    {
        var jobbyBuilder = new JobbyBuilder();
        return jobbyBuilder.CreateJobsFactory();
    }
}
