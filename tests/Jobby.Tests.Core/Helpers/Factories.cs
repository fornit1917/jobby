using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Services;
using Jobby.Core.Services.Schedulers.CronSimple;

namespace Jobby.Tests.Core.Helpers;
internal static class Factories
{
    public static IJobsFactory CreateJobsFactory()
    {
        return new JobbyBuilder().CreateJobsFactory();
    }

    public static CronSimpleScheduler CRON_SIMPLE_SCHEDULER(string cron)
    {
        var cronExpression = CronHelper.Parse(cron);
        return new CronSimpleScheduler(cronExpression);
    }
}
