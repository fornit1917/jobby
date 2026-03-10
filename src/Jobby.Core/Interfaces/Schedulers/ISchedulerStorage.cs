namespace Jobby.Core.Interfaces.Schedulers;

public interface ISchedulerStorage
{
    string DefaultSchedulerType { get; }
}

public interface ISchedulerStorage<TScheduler> : ISchedulerStorage
    where TScheduler : IScheduler
{
    IScheduleSerializer<TScheduler> Serializer { get; }
}
