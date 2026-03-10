using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces.Schedulers;
using Jobby.Core.Services.Schedulers.Cron;
using Jobby.Core.Services.Schedulers.CronSimple;
using System.Collections.Frozen;

namespace Jobby.Core.Services.Schedulers;
internal partial class SchedulersRegistry
{
    public class Builder
    {
        private readonly string? _defaultSchedulerType;
        private readonly Dictionary<string, ISchedulerExecutor> _schedulersByType = new();
        private readonly Dictionary<Type, ISchedulerStorage> _schedulersBySchedulerType = new();

        public Builder()
        {
            const string DEFAULT_SCHEDULERS_PREFIX = "__JOBBY_";

            AddScheduler(new CronSimpleStorage(), DEFAULT_SCHEDULERS_PREFIX, out _defaultSchedulerType);
            AddScheduler(new CronStorage(), DEFAULT_SCHEDULERS_PREFIX);
        }

        public Builder AddScheduler<TScheduler>(ISchedulerStorage<TScheduler> schedulerStorage, string? schedulerTypePrefix = null)
            where TScheduler : IScheduler
        {
            return AddScheduler<TScheduler>(schedulerStorage, schedulerTypePrefix, out var _);
        }

        private Builder AddScheduler<TScheduler>(ISchedulerStorage<TScheduler> schedulerStorage, string? schedulerTypePrefix, out string schedulerType)
            where TScheduler : IScheduler
        {
            schedulerType = schedulerTypePrefix is null ?
                schedulerStorage.DefaultSchedulerType :
                $"{schedulerTypePrefix}{schedulerStorage.DefaultSchedulerType}";

            if (!_schedulersByType.TryAdd(schedulerType, new ScheduleExecutor<TScheduler>(schedulerStorage.Serializer)))
                throw new InvalidJobsConfigException($"Scheduler for {schedulerType} has already been added");

            if (!_schedulersBySchedulerType.TryAdd(typeof(TScheduler), new SchedulerStorage<TScheduler>(schedulerType, schedulerStorage.Serializer)))
                throw new InvalidJobsConfigException($"Scheduler for type {typeof(TScheduler)} has already been added");

            return this;
        }

        private SchedulersRegistry? result;

        internal SchedulersRegistry Build()
        {
            if (result is null)
                result = new SchedulersRegistry(
                    _defaultSchedulerType ?? throw new Exception("Default scheduler type was not specified"),
                    _schedulersByType.ToFrozenDictionary(),
                    _schedulersBySchedulerType.ToFrozenDictionary()
                );

            return result;
        }
    }
}
