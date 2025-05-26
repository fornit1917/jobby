using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IJobParamSerializer _serializer;

    public JobsFactory(IJobParamSerializer serializer)
    {
        _serializer = serializer;
    }

    public Job Create<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
        };
    }

    public Job Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = startTime,
            Status = JobStatus.Scheduled,
        };
    }

    public Job CreateRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            JobParam = _serializer.SerializeJobParam(command),
            JobName = TCommand.GetJobName(),
            Cron = cron,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = CronHelper.GetNext(cron, DateTime.UtcNow),
        };
    }

    public JobsSequenceBuilder CreateSequenceBuilder()
    {
        return new JobsSequenceBuilder(this);
    }

    public JobsSequenceBuilder CreateSequenceBuilder(int capacity)
    {
        return new JobsSequenceBuilder(capacity, this);
    }
}
