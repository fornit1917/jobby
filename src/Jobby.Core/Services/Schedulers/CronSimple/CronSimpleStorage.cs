using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.CronSimple;
internal class CronSimpleStorage : ISchedulerStorage<CronSimpleScheduler>
{
    public string DefaultSchedulerType => "CRON_SIMPLE";
    public IScheduleSerializer<CronSimpleScheduler> Serializer { get; } = new CronSimpleSchedulerSerializer();

}
