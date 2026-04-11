using Jobby.Core.Models;
using Jobby.IntegrationTests.Postgres.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.PostgresqlJobbyStorageTests;

[Collection(PostgresqlTestsCollection.Name)]
public class UpdateProcessingJobToCompletedTests
{
    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_NoNextJob_UpdatesCurrent()
    {
        await using var dbContext = DbHelper.CreateContext();

        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            Schedule = null,
            JobParam = "param",
            StartedCount = 2,
            NextJobId = null,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            Error = "prev error",
            ServerId = Guid.NewGuid().ToString(),
        };
        await dbContext.AddAsync(job, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToJobExecutionModel());

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_HasNextJob_CompletesCurrentAndSchedulesNext()
    {
        await using var dbContext = DbHelper.CreateContext();
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.WaitingPrev,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            StartedCount = 2,
            NextJobId = nextJob.Id,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = Guid.NewGuid().ToString(),
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToJobExecutionModel());

        var actualJob = await dbContext.Jobs.AsNoTracking().FirstAsync(x => x.Id == job.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Completed, actualJob.Status);
        Assert.NotNull(actualJob.LastFinishedAt);
        Assert.Equal(DateTime.UtcNow, actualJob.LastFinishedAt.Value, TimeSpan.FromSeconds(3));
        Assert.Null(actualJob.Error);

        var actualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.NextJobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Scheduled, actualNextJob.Status);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_CurrentNotProcessing_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.WaitingPrev,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            StartedCount = 2,
            NextJobId = nextJob.Id,
            Status = JobStatus.Failed,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = Guid.NewGuid().ToString(),
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        await storage.UpdateProcessingJobToCompletedAsync(job.ToJobExecutionModel());

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, actualJob.Status);

        var actualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.NextJobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }

    [Fact]
    public async Task UpdateProcessingJobToCompletedAsync_CurrentOnOtherServer_DoesNothing()
    {
        await using var dbContext = DbHelper.CreateContext();
        var nextJob = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            Status = JobStatus.WaitingPrev,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
        };
        var job = new JobDbModel
        {
            Id = Guid.NewGuid(),
            JobName = Guid.NewGuid().ToString(),
            JobParam = "param",
            StartedCount = 2,
            NextJobId = nextJob.Id,
            Status = JobStatus.Processing,
            ScheduledStartAt = DateTime.UtcNow.AddDays(1),
            ServerId = "new_server",
            Error = "prev error"
        };
        await dbContext.AddRangeAsync([nextJob, job]);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = DbHelper.CreateJobbyStorage();
        job.ServerId = "old_server";
        await storage.UpdateProcessingJobToCompletedAsync(job.ToJobExecutionModel());

        var actualJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.Id,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Processing, actualJob.Status);

        var actualNextJob = await dbContext.Jobs.AsNoTracking()
            .FirstAsync(x => x.Id == job.NextJobId,
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.WaitingPrev, actualNextJob.Status);
    }    
}