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
        private readonly Dictionary<string, ISchedulerExecutor> _schedulersByType = new();
        private readonly Dictionary<Type, ISchedulerStorage> _schedulersBySchedulerType = new();

        public Builder()
        {
            const string DEFAULT_SCHEDULERS_PREFIX = "__JOBBY_";

            AddScheduler(new CronSimpleStorage(), DEFAULT_SCHEDULERS_PREFIX);
            AddScheduler(new CronStorage(), DEFAULT_SCHEDULERS_PREFIX);
        }

        public Builder AddScheduler<TScheduler>(ISchedulerStorage<TScheduler> schedulerStorage, string? prefix = null)
            where TScheduler : IScheduler
        {
            var schedulerType = prefix is null ?
                schedulerStorage.DefaultSchedulerType :
                $"{prefix}{schedulerStorage.DefaultSchedulerType}";

            if (!_schedulersByType.TryAdd(schedulerType, new ScheduleExecutor<TScheduler>(schedulerStorage.Serializer)))
                throw new InvalidJobsConfigException($"Scheduler for {schedulerType} has already been added");

            if (!_schedulersBySchedulerType.TryAdd(typeof(TScheduler), schedulerStorage))
                throw new InvalidJobsConfigException($"Scheduler for type {typeof(TScheduler)} has already been added");

            return this;
        }

        private SchedulersRegistry? result;

        internal SchedulersRegistry Build()
        {
            if (result is null)
                result = new SchedulersRegistry(
                    _schedulersByType.ToFrozenDictionary(),
                    _schedulersBySchedulerType.ToFrozenDictionary()
                );

            return result;
        }
    }
}
