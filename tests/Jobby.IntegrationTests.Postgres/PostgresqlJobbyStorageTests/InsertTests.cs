using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.TestsUtils.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class InsertTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task InsertAsync_Inserts(bool isExclusiveRecurrent, bool lockIfFailedNotRecurrent)
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var factory = CreateJobsFactory();
        
        var firstJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *", new RecurrentJobOpts
        {
            IsExclusive = isExclusiveRecurrent,
            CanBeRestartedIfServerGoesDown = true,
            QueueName = "q1",
            SerializableGroupId = "gid",
            StartTime = DateTime.UtcNow.AddDays(100)
        });

        var jobs = factory.CreateSequenceBuilder()
            .Add(new TestJobCommand { UniqueId = Guid.NewGuid() }, new JobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                QueueName = "q2",
                SerializableGroupId = null,
                LockGroupIfFailed = lockIfFailedNotRecurrent,
                StartTime = DateTime.UtcNow.AddDays(200),
            })
            .Add(new TestJobCommand { UniqueId = Guid.NewGuid() }, new JobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                QueueName = "q2",
                SerializableGroupId = null,
                LockGroupIfFailed = lockIfFailedNotRecurrent,
                StartTime = DateTime.UtcNow.AddDays(200),
            })
            .GetJobs();

        var secondJob = jobs[0];
        var thirdJob = jobs[1];

        var storage = DbHelper.CreateJobbyStorage();
        var firstJobId = await storage.InsertJobAsync(firstJob);
        var secondJobId = await storage.InsertJobAsync(secondJob);
        var thirdJobId = await storage.InsertJobAsync(thirdJob);

        var firstActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == firstJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        var secondActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == secondJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        var thirdActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == thirdJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        Assert.NotNull(thirdActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
        AssertHelper.AssertCreatedJob(thirdJob, thirdActualJob);
        Assert.Equal(firstActualJob.Id, firstJobId);
        Assert.Equal(secondActualJob.Id, secondJobId);
        Assert.Equal(thirdActualJob.Id, thirdJobId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Insert_Inserts(bool isExclusiveRecurrent, bool lockIfFailedNotRecurrent)
    {
        using var dbContext = DbHelper.CreateContextAndClearDb();
        
        var factory = CreateJobsFactory();
        
        var firstJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *", new RecurrentJobOpts
        {
            IsExclusive = isExclusiveRecurrent,
            CanBeRestartedIfServerGoesDown = true,
            QueueName = "q1",
            SerializableGroupId = "gid",
            StartTime = DateTime.UtcNow.AddDays(100)
        });

        var jobs = factory.CreateSequenceBuilder()
            .Add(new TestJobCommand { UniqueId = Guid.NewGuid() }, new JobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                QueueName = "q2",
                SerializableGroupId = null,
                LockGroupIfFailed = lockIfFailedNotRecurrent,
                StartTime = DateTime.UtcNow.AddDays(200),
            })
            .Add(new TestJobCommand { UniqueId = Guid.NewGuid() }, new JobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                QueueName = "q2",
                SerializableGroupId = null,
                LockGroupIfFailed = lockIfFailedNotRecurrent,
                StartTime = DateTime.UtcNow.AddDays(200),
            })
            .GetJobs();

        var secondJob = jobs[0];
        var thirdJob = jobs[1];

        var storage = DbHelper.CreateJobbyStorage();
        var firstJobId = storage.InsertJob(firstJob);
        var secondJobId = storage.InsertJob(secondJob);
        var thirdJobId = storage.InsertJob(thirdJob);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);
        var thirdActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == thirdJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        Assert.NotNull(thirdActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
        AssertHelper.AssertCreatedJob(thirdJob, thirdActualJob);
        Assert.Equal(firstActualJob.Id, firstJobId);
        Assert.Equal(secondActualJob.Id, secondJobId);
        Assert.Equal(thirdActualJob.Id, thirdJobId);
    }

    [Fact]
    public async Task InsertAsync_RecurrentExclusiveExisting_UpdatesRecurrent()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        var factory = CreateJobsFactory();

        var job = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                CanBeRestartedIfServerGoesDown = true,
                StartTime = DateTime.UtcNow.AddDays(100),
                QueueName = "q1",
                SerializableGroupId = "old_gid"
            });
        
        var newJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                StartTime = DateTime.UtcNow.AddDays(200),
                QueueName = "q2",
                SerializableGroupId = "new_gid"
            });

        var storage = DbHelper.CreateJobbyStorage();
        var firstJobId = await storage.InsertJobAsync(job);
        var secondJobId = await storage.InsertJobAsync(newJob);

        Assert.Equal(firstJobId, secondJobId);
        Assert.Equal(job.Id, firstJobId);
        
        var actualJobWithOldId = await dbContext.Jobs
            .FirstOrDefaultAsync(x => x.Id == job.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJobWithOldId);
        
        var actualJobWithNewId = await dbContext.Jobs
            .FirstOrDefaultAsync(x => x.Id == newJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(actualJobWithNewId);
        
        AssertHelper.AssertCreatedJobExceptId(newJob, actualJobWithOldId);
    }

    [Fact]
    public void Insert_RecurrentExclusiveExisting_UpdatesRecurrent()
    {
        using var dbContext = DbHelper.CreateContextAndClearDb();
        
        var factory = CreateJobsFactory();

        var job = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                CanBeRestartedIfServerGoesDown = true,
                StartTime = DateTime.UtcNow.AddDays(100),
                QueueName = "q1",
                SerializableGroupId = "old_gid"
            });
        
        var newJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/5 * * * *",
            new RecurrentJobOpts
            {
                CanBeRestartedIfServerGoesDown = false,
                StartTime = DateTime.UtcNow.AddDays(200),
                QueueName = "q2",
                SerializableGroupId = "new_gid"
            });

        var storage = DbHelper.CreateJobbyStorage();
        var firstJobId = storage.InsertJob(job);
        var secondJobId = storage.InsertJob(newJob);
        
        Assert.Equal(firstJobId, secondJobId);
        Assert.Equal(job.Id, firstJobId);

        var actualJobWithOldId = dbContext.Jobs.FirstOrDefault(x => x.Id == job.Id);
        Assert.NotNull(actualJobWithOldId);
        var actualJobWithNewId = dbContext.Jobs.FirstOrDefault(x => x.Id == newJob.Id);
        Assert.Null(actualJobWithNewId);
        AssertHelper.AssertCreatedJobExceptId(newJob, actualJobWithOldId);
    }
    
    [Fact]
    public async Task InsertAsync_RecurrentNotExclusiveExisting_CreatesSecond()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();
        
        var factory = CreateJobsFactory();

        var job = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                IsExclusive = false,
                CanBeRestartedIfServerGoesDown = true,
                StartTime = DateTime.UtcNow.AddDays(100),
                QueueName = "q1",
                SerializableGroupId = "old_gid"
            });
        
        var newJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                IsExclusive = false,
                CanBeRestartedIfServerGoesDown = false,
                StartTime = DateTime.UtcNow.AddDays(200),
                QueueName = "q2",
                SerializableGroupId = "new_gid"
            });

        var storage = DbHelper.CreateJobbyStorage();
        await storage.InsertJobAsync(job);
        await storage.InsertJobAsync(newJob);

        var actualJobWithOldId = await dbContext.Jobs
            .FirstOrDefaultAsync(x => x.Id == job.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJobWithOldId);
        AssertHelper.AssertCreatedJob(job, actualJobWithOldId);
        
        var actualJobWithNewId = await dbContext.Jobs
            .FirstOrDefaultAsync(x => x.Id == newJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(actualJobWithNewId);
        AssertHelper.AssertCreatedJob(newJob, actualJobWithNewId);
    }

    [Fact]
    public void Insert_RecurrentNotExclusiveExisting_CreatesSecond()
    {
        using var dbContext = DbHelper.CreateContextAndClearDb();
        
        var factory = CreateJobsFactory();

        var job = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                IsExclusive = false,
                CanBeRestartedIfServerGoesDown = true,
                StartTime = DateTime.UtcNow.AddDays(100),
                QueueName = "q1",
                SerializableGroupId = "old_gid"
            });
        
        var newJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *",
            new RecurrentJobOpts
            {
                IsExclusive = false,
                CanBeRestartedIfServerGoesDown = false,
                StartTime = DateTime.UtcNow.AddDays(200),
                QueueName = "q2",
                SerializableGroupId = "new_gid"
            });

        var storage = DbHelper.CreateJobbyStorage();
        storage.InsertJob(job);
        storage.InsertJob(newJob);

        var actualJobWithOldId = dbContext.Jobs.FirstOrDefault(x => x.Id == job.Id);
        Assert.NotNull(actualJobWithOldId);
        AssertHelper.AssertCreatedJob(job, actualJobWithOldId);
        
        var actualJobWithNewId = dbContext.Jobs.FirstOrDefault(x => x.Id == newJob.Id);
        Assert.NotNull(actualJobWithNewId);
        AssertHelper.AssertCreatedJob(newJob, actualJobWithNewId);
    }
    
    private IJobsFactory CreateJobsFactory()
    {
        return new JobbyBuilder().CreateJobsFactory();
    }
}