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
            AddScheduler<CronSimpleSchedule, CronSimpleScheduleHandler, CronSimpleScheduleSerializer>();
            AddScheduler<CronSchedule, CronScheduleHandler, CronScheduleSerializer>();
        }

        public Builder AddScheduler<TScheduler, TSchedulerStorage>(string? prefix = null)
            where TScheduler : IScheduler
            where TSchedulerStorage : ISchedulerStorage<TScheduler>, new()
            => AddScheduler<TScheduler>(new TSchedulerStorage(), prefix);

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

        internal SchedulersRegistry Build() => new SchedulersRegistry(
            _schedulersByType.ToFrozenDictionary(),
            _schedulersBySchedulerType.ToFrozenDictionary()
        );
    }
}
