namespace Jobby.Core.Interfaces.Schedulers;
internal interface ISchedulerStorageOptionsProvider
{
    public (string schedulerType, string schedulerOptions) GetStorageOptions<TScheduler>(TScheduler scheduler)
        where TScheduler : IScheduler;
}
