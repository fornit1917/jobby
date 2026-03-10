using Microsoft.EntityFrameworkCore;

using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.TestsUtils.Jobs;

using static Jobby.IntegrationTests.Postgres.Helpers.Factories;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class BulkInsertTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task BulkInsertAsync_Inserts(bool isExclusiveRecurrent, bool lockIfFailedNotRecurrent)
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var factory = CreateJobsFactory();
        
        var firstJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, CRON_SIMPLE_SCHEDULE("*/10 * * * *"), new RecurrentJobOpts
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

        await storage.BulkInsertJobsAsync([firstJob, secondJob, thirdJob]);

        var firstActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firstJob.Id);
        var secondActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == secondJob.Id);
        var thirdActualJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == thirdJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        Assert.NotNull(thirdActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
        AssertHelper.AssertCreatedJob(thirdJob, thirdActualJob);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BulkInsert_Inserts(bool isExclusiveRecurrent, bool lockIfFailedNotRecurrent)
    {
        using var dbContext = DbHelper.CreateContextAndClearDb();

        var factory = CreateJobsFactory();
        
        var firstJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, CRON_SIMPLE_SCHEDULE("*/10 * * * *"), new RecurrentJobOpts
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
        storage.BulkInsertJobs([firstJob, secondJob, thirdJob]);

        var firstActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == firstJob.Id);
        var secondActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == secondJob.Id);
        var thirdActualJob = dbContext.Jobs.AsNoTracking().FirstOrDefault(x => x.Id == thirdJob.Id);

        Assert.NotNull(firstActualJob);
        Assert.NotNull(secondActualJob);
        Assert.NotNull(thirdActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);
        AssertHelper.AssertCreatedJob(secondJob, secondActualJob);
        AssertHelper.AssertCreatedJob(thirdJob, thirdActualJob);
    }
}