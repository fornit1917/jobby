namespace Jobby.Core.Interfaces.Schedulers;
internal interface ISchedulerStorage<TScheduler> where TScheduler : IScheduler
{
    string DefaultSchedulerType { get; }
    IScheduleSerializer<TScheduler> Serializer { get; }
}
