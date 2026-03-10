using System.Collections.Frozen;

using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;
internal class SchedulersRegistry
{
    private readonly FrozenDictionary<string, ISchedulerExecutor> _schedulersByType;
    private readonly FrozenDictionary<Type, ISchedulerExecutor> _schedulersBySchedulerType;

    private SchedulersRegistry(FrozenDictionary<string, ISchedulerExecutor> schedulersByKey, FrozenDictionary<Type, ISchedulerExecutor> schedulersByType)
    {
        _schedulersByType = schedulersByKey ?? throw new ArgumentNullException(nameof(schedulersByKey));
        _schedulersBySchedulerType = schedulersByType ?? throw new ArgumentNullException(nameof(schedulersByType));
    }


    public ISchedulerExecutor GetExecutor(string schedulerType)
    {
        if (!_schedulersByType.TryGetValue(schedulerType, out var schedulerExecutor))
            throw new UnknownSchedulerTypeException($"Unknown scheduler type: {schedulerType}");

        return schedulerExecutor;
    }

    public (string schedulerType, string schedulerOptions) GetStorageOptions<TScheduler>(TScheduler scheduler) where TScheduler : IScheduler
    {
        var schedulerType = typeof(TScheduler);
        if (!_schedulersBySchedulerType.TryGetValue(schedulerType, out var schedulerExecutor))
            throw new UnknownSchedulerTypeException($"Unknown scheduler type: {schedulerType}");

        if (scheduler is not SchedulerStorage<TScheduler> { } schedulerStorage)
            throw new Exception($"Invalid scheduler executor for scheduler type {schedulerType}. Expected {typeof(ISchedulerStorage<TScheduler>)} but found {scheduler.GetType()}");

        var schedulerOptions = schedulerStorage.Serializer.Serealize(scheduler);

        return (schedulerStorage.SchedulerType, schedulerOptions);
    }

    private readonly record struct SchedulerStorage<TScheduler>(string SchedulerType, IScheduleSerializer<TScheduler> Serializer)
        where TScheduler : IScheduler;
}
