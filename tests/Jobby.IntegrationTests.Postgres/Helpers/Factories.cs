using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Core.Services.Schedulers.CronSimple;

namespace Jobby.IntegrationTests.Postgres.Helpers;
internal static class Factories
{
    public static IJobsFactory CreateJobsFactory()
    {
        return new JobbyBuilder().CreateJobsFactory();
    }

    public static CronSimpleSchedule CRON_SIMPLE_SCHEDULE(string cron)
    {
        var cronExpression = CronHelper.Parse(cron);
        return new CronSimpleSchedule(cronExpression);
    }
}
