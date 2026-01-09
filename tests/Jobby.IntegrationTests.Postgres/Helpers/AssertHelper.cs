using Jobby.Core.Models;

namespace Jobby.IntegrationTests.Postgres.Helpers;

internal static class AssertHelper
{
    public static void AssertCreatedJob(JobCreationModel expected, JobDbModel actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Cron, actual.Cron);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.JobName, actual.JobName);
        Assert.Equal(expected.CanBeRestarted, actual.CanBeRestarted);
        Assert.Equal(expected.NextJobId, actual.NextJobId);
        Assert.Equal(expected.ScheduledStartAt, actual.ScheduledStartAt, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.JobParam, actual.JobParam);
        Assert.Null(actual.Error);
        Assert.Null(actual.LastStartedAt);
        Assert.Null(actual.LastFinishedAt);
        Assert.Equal(0, actual.StartedCount);
        Assert.Null(actual.ServerId);
    }    
}