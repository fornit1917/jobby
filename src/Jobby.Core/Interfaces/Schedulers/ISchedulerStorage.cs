namespace Jobby.Core.Interfaces.Schedulers;

public interface ISchedulerStorage
{
    string SchedulerType { get; }
}

public interface ISchedulerStorage<TScheduler>
    where TScheduler : IScheduler
{
    string DefaultSchedulerType { get; }
    IScheduleSerializer<TScheduler> Serializer { get; }
}
