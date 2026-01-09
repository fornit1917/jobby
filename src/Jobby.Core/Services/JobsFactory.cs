using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IJobParamSerializer _serializer;

    public JobsFactory(IGuidGenerator guidGenerator, IJobParamSerializer serializer)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
    }

    public JobCreationModel Create<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            CanBeRestarted = command.CanBeRestarted()
        };
    }

    public JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = startTime,
            Status = JobStatus.Scheduled,
            CanBeRestarted = command.CanBeRestarted()
        };
    }

    public JobCreationModel CreateRecurrent<TCommand>(TCommand command, string cron) where TCommand : IJobCommand
    {
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            JobParam = _serializer.SerializeJobParam(command),
            JobName = TCommand.GetJobName(),
            Cron = cron,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = CronHelper.GetNext(cron, DateTime.UtcNow),
            CanBeRestarted = command.CanBeRestarted()
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
