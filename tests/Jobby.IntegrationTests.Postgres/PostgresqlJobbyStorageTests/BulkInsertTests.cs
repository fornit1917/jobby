using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Core.Services.Observability;
using Jobby.IntegrationTests.Postgres.Helpers;
using Jobby.TestsUtils.Jobs;
using Jobby.TestsUtils.Tracing;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

        await storage.BulkInsertJobsAsync([firstJob, secondJob, thirdJob]);

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
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BulkInsert_Inserts(bool isExclusiveRecurrent, bool lockIfFailedNotRecurrent)
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

    [Fact]
    public async Task BulkInsertAsync_RepeatInsert_WithNewTime_ShouldNotChangeScheduledStartTime()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var factory = CreateJobsFactory();

        var firstJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *", new RecurrentJobOpts
        {
            IsExclusive = true,
            CanBeRestartedIfServerGoesDown = true,
            QueueName = "q1",
            SerializableGroupId = "gid1",
            StartTime = DateTime.UtcNow.AddDays(100)
        });

        var secondJob = factory.CreateRecurrent(new TestJobCommand { UniqueId = Guid.NewGuid() }, "*/10 * * * *", new RecurrentJobOpts
        {
            IsExclusive = true,
            CanBeRestartedIfServerGoesDown = true,
            QueueName = "q2",
            SerializableGroupId = "gid2",
            StartTime = DateTime.UtcNow.AddDays(200)
        });

        var storage = DbHelper.CreateJobbyStorage();

        await storage.BulkInsertJobsAsync([firstJob]);

        var firstActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == firstJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(firstActualJob);
        AssertHelper.AssertCreatedJob(firstJob, firstActualJob);

        await storage.BulkInsertJobsAsync([secondJob]);

        firstActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == firstJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        var secondActualJob = await dbContext.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == secondJob.Id,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(secondActualJob);
        Assert.NotNull(firstActualJob);
        AssertHelper.AssertCreatedJobExceptIdAndStartTime(secondJob, firstActualJob);
        Assert.Equal(firstActualJob.ScheduledStartAt, firstJob.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.Equal(firstActualJob.Id, firstJob.Id);
    }

    [Fact]
    public async Task BulkInsertAsync_TracingModeRestore_SavesTraceparentAndModeToDb()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var builder = new JobbyBuilder();
        builder.UseTracing(new JobbyTracingSettings { TracingMode = JobbyTracingMode.RestoreTraceId });
        var factory = builder.CreateJobsFactory();

        var activity = TestTracingContext.StartAmbientActivity("bulk-insert-test");
        try
        {
            var expectedTraceparent = TestTracingContext.ToTraceparent(activity);
            var firstJob = factory.Create(new TestJobCommand { UniqueId = Guid.NewGuid() },
                new JobOpts { StartTime = DateTime.UtcNow.AddDays(1) });
            var secondJob = factory.Create(new TestJobCommand { UniqueId = Guid.NewGuid() },
                new JobOpts { StartTime = DateTime.UtcNow.AddDays(2) });
            Assert.Equal(expectedTraceparent, firstJob.Traceparent);
            Assert.Equal(expectedTraceparent, secondJob.Traceparent);

            var storage = DbHelper.CreateJobbyStorage();
            await storage.BulkInsertJobsAsync([firstJob, secondJob]);

            var firstActualJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == firstJob.Id, cancellationToken: TestContext.Current.CancellationToken);
            var secondActualJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == secondJob.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(expectedTraceparent, firstActualJob.Traceparent);
            Assert.Equal(expectedTraceparent, secondActualJob.Traceparent);
            Assert.Equal(JobbyTracingMode.RestoreTraceId, firstActualJob.TracingMode);
            Assert.Equal(JobbyTracingMode.RestoreTraceId, secondActualJob.TracingMode);
        }
        finally
        {
            Activity.Current = null;
        }
    }

    [Fact]
    public async Task BulkInsertAsync_TracingModeCreateNewViaOpts_SavesNullTraceparent()
    {
        await using var dbContext = await DbHelper.CreateContextAndClearDbAsync();

        var builder = new JobbyBuilder();
        builder.UseTracing(new JobbyTracingSettings { TracingMode = JobbyTracingMode.RestoreTraceId });
        var factory = builder.CreateJobsFactory();

        // ambient activity exists, but tracing context capture is disabled for these jobs via JobOpts
        var activity = TestTracingContext.StartAmbientActivity("bulk-insert-test");
        try
        {
            var firstJob = factory.Create(new TestJobCommand { UniqueId = Guid.NewGuid() },
                new JobOpts { StartTime = DateTime.UtcNow.AddDays(1), TracingMode = JobbyTracingMode.CreateNewTraceId });
            var secondJob = factory.Create(new TestJobCommand { UniqueId = Guid.NewGuid() },
                new JobOpts { StartTime = DateTime.UtcNow.AddDays(2), TracingMode = JobbyTracingMode.CreateNewTraceId });
            Assert.Null(firstJob.Traceparent);
            Assert.Null(secondJob.Traceparent);

            var storage = DbHelper.CreateJobbyStorage();
            await storage.BulkInsertJobsAsync([firstJob, secondJob]);

            var firstActualJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == firstJob.Id, cancellationToken: TestContext.Current.CancellationToken);
            var secondActualJob = await dbContext.Jobs.AsNoTracking()
                .FirstAsync(x => x.Id == secondJob.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(firstActualJob.Traceparent);
            Assert.Null(secondActualJob.Traceparent);
            Assert.Equal(JobbyTracingMode.CreateNewTraceId, firstActualJob.TracingMode);
            Assert.Equal(JobbyTracingMode.CreateNewTraceId, secondActualJob.TracingMode);
        }
        finally
        {
            Activity.Current = null;
        }
    }

    private IJobsFactory CreateJobsFactory()
    {
        return new JobbyBuilder().CreateJobsFactory();
    }
}