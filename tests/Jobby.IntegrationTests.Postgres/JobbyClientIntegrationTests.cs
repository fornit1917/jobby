using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Core.Services.Schedulers;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres;

[Collection(PostgresqlTestsCollection.Name)]
public class JobbyClientIntegrationTests
{
    [Fact]
    public async Task DefaultOpts_EnqueuesAndCancelsCommandByAsyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var jobId = await client.EnqueueCommandAsync(command, DateTime.UtcNow.AddDays(1));

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(QueueSettings.DefaultQueueName, actualJob.QueueName);

        await client.CancelJobsByIdsAsync(jobId);
        actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualJob);
    }

    [Fact]
    public void DefaultOpts_EnqueuesAndCancelsCommandBySyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var jobId = client.EnqueueCommand(command, DateTime.UtcNow.AddDays(1));

        var actualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == jobId);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(QueueSettings.DefaultQueueName, actualJob.QueueName);

        client.CancelJobsByIds(jobId);
        actualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == jobId);
        Assert.Null(actualJob);
    }
    
    [Fact]
    public async Task SpecifiedOptions_EnqueuesAndCancelsCommandByAsyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var opts = new JobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(1),
            SerializableGroupId = "gid",
            LockGroupIfFailed = true
        };
        var jobId = await client.EnqueueCommandAsync(command, opts);

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(opts.QueueName, actualJob.QueueName);
        Assert.Equal(opts.SerializableGroupId, actualJob.SerializableGroupId);
        Assert.Equal(opts.LockGroupIfFailed, actualJob.LockGroupIfFailed);
        Assert.Equal(opts.StartTime.Value, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));

        await client.CancelJobsByIdsAsync(jobId);
        actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualJob);
    }

    [Fact]
    public void SpecifiedOptions_EnqueuesAndCancelsCommandBySyncMethods()
    {
        var dbContext = DbHelper.CreateContext();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var opts = new JobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(1),
            SerializableGroupId = "gid",
            LockGroupIfFailed = true
        };
        var jobId = client.EnqueueCommand(command, opts);

        var actualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == jobId);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(opts.QueueName, actualJob.QueueName);
        Assert.Equal(opts.SerializableGroupId, actualJob.SerializableGroupId);
        Assert.Equal(opts.LockGroupIfFailed, actualJob.LockGroupIfFailed);
        Assert.Equal(opts.StartTime.Value, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));

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
        var actualJobsFromDb = await dbContext.Jobs.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, actualJobsFromDb.Count);

        await client.CancelJobsByIdsAsync(ids);
        actualJobsFromDb = await dbContext.Jobs.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
    public async Task DefaultOpts_SchedulesRecurrentAndCancelsByAsyncMethods()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        await client.ScheduleRecurrentAsync(command, cron);

        var actualJobFromDb = await dbContext.Jobs.AsNoTracking()
                                             .Where(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron)
                                             .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJobFromDb);
        Assert.Equal(JobbySchedulerTypes.CronFromNow, actualJobFromDb.SchedulerType);

        await client.CancelRecurrentAsync<TestJobCommand>();

        actualJobFromDb = await dbContext.Jobs.AsNoTracking()
                                             .Where(x => x.JobName == TestJobCommand.GetJobName())
                                             .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualJobFromDb);
    }

    [Fact]
    public void DefaultOpts_SchedulesRecurrentAndCancelsBySyncMethods()
    {
        var dbContext = DbHelper.CreateContextAndClearDb();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        client.ScheduleRecurrent(command, cron, JobbySchedulerTypes.CronFromPrev);

        var actualJobFromDb = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron);
        Assert.NotNull(actualJobFromDb);
        Assert.Equal(JobbySchedulerTypes.CronFromPrev, actualJobFromDb.SchedulerType);

        client.CancelRecurrent<TestJobCommand>();

        actualJobFromDb = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName());
        Assert.Null(actualJobFromDb);
    }
    
    [Fact]
    public async Task SpecifiedOpts_SchedulesRecurrentAndCancelsByAsyncMethods()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        var opts = new RecurrentJobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(3),
            SerializableGroupId = "gid"
        };
        await client.ScheduleRecurrentAsync(command, cron, opts);

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(opts.QueueName, actualJob.QueueName);
        Assert.Equal(opts.SerializableGroupId, actualJob.SerializableGroupId);
        Assert.Equal(opts.StartTime.Value, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));        
        
        await client.CancelRecurrentAsync<TestJobCommand>();

        actualJob = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.JobName == TestJobCommand.GetJobName())
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualJob);
    }

    [Fact]
    public void SpecifiedOpts_SchedulesRecurrentAndCancelsBySyncMethods()
    {
        var dbContext = DbHelper.CreateContextAndClearDb();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        var opts = new RecurrentJobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(3),
            SerializableGroupId = "gid"
        };
        client.ScheduleRecurrent(command, cron, opts);

        var actualJob = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron);
        Assert.NotNull(actualJob);
        Assert.Equal(TestJobCommand.GetJobName(), actualJob.JobName);
        Assert.Equal(opts.QueueName, actualJob.QueueName);
        Assert.Equal(opts.SerializableGroupId, actualJob.SerializableGroupId);
        Assert.Equal(opts.StartTime.Value, actualJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
        client.CancelRecurrent<TestJobCommand>();

        actualJob = dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefault(x => x.JobName == TestJobCommand.GetJobName());
        Assert.Null(actualJob);
    }
    
    [Fact]
    public async Task SpecifiedOpts_NotExclusive_SchedulesFewRecurrentAsyncMethods()
    {
        var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        var opts = new RecurrentJobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(3),
            SerializableGroupId = "gid",
            IsExclusive = false,
        };
        await client.ScheduleRecurrentAsync(command, cron, opts);
        await client.ScheduleRecurrentAsync(command, cron, opts);

        var actualJobs = await dbContext.Jobs.AsNoTracking()
            .Where(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, actualJobs.Count);
    }

    [Fact]
    public void SpecifiedOpts_NotExclusive_SchedulesFewRecurrentBySyncMethods()
    {
        var dbContext = DbHelper.CreateContextAndClearDb();
        var client = CreateJobbyClient();

        var command = new TestJobCommand();
        var cron = "0 3 1 1 *";
        var opts = new RecurrentJobOpts
        {
            QueueName = "CustomQueue",
            StartTime = DateTime.UtcNow.AddDays(3),
            SerializableGroupId = "gid",
            IsExclusive = false,
        };
        client.ScheduleRecurrent(command, cron, opts);
        client.ScheduleRecurrent(command, cron, opts);

        var actualJobs = dbContext.Jobs
            .AsNoTracking()
            .Where(x => x.JobName == TestJobCommand.GetJobName() && x.Schedule == cron)
            .ToList();
        Assert.Equal(2, actualJobs.Count);
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
