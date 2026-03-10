using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Schedulers;
using System.Collections.Frozen;

namespace Jobby.Core.Services.Schedulers;
internal partial class SchedulersRegistry
{
    private readonly FrozenDictionary<string, ISchedulerExecutor> _schedulersByType;
    private readonly FrozenDictionary<Type, ISchedulerStorage> _schedulersBySchedulerType;

    private SchedulersRegistry(FrozenDictionary<string, ISchedulerExecutor> schedulersByKey, FrozenDictionary<Type, ISchedulerStorage> schedulersByType)
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
