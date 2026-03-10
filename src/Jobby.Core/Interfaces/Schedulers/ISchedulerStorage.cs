namespace Jobby.Core.Interfaces.Schedulers;

internal interface ISchedulerStorage
{
    string DefaultSchedulerType { get; }
}

internal interface ISchedulerStorage<TScheduler> : ISchedulerStorage
    where TScheduler : IScheduler
{
    IScheduleSerializer<TScheduler> Serializer { get; }
}
