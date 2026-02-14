using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class JobsFactory : IJobsFactory
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IJobParamSerializer _serializer;
    private readonly IQueueNameAssignor _queueNameAssignor;

    public JobsFactory(IGuidGenerator guidGenerator,
        IJobParamSerializer serializer,
        IQueueNameAssignor queueNameAssignor)
    {
        _guidGenerator = guidGenerator;
        _serializer = serializer;
        _queueNameAssignor = queueNameAssignor;
    }

    public JobCreationModel Create<TCommand>(TCommand command, JobOpts opts = default)
        where TCommand : IJobCommand
    {
        var jobName = TCommand.GetJobName();
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            JobName = jobName,
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = opts.StartTime ?? DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            CanBeRestarted = command.CanBeRestarted(),
            QueueName = _queueNameAssignor.GetQueueName(jobName, opts),
            SerializableGroupId = opts.SerializableGroupId,
            LockGroupIfFailed = opts.LockGroupIfFailed ?? false
        };    
    }

    public JobCreationModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        return Create(command, new JobOpts { StartTime = startTime });
    }

    public JobCreationModel CreateRecurrent<TCommand>(TCommand command, string cron, RecurrentJobOpts opts = default)
        where TCommand : IJobCommand
    {
        var jobName = TCommand.GetJobName();
        return new JobCreationModel
        {
            Id = _guidGenerator.NewGuid(),
            JobParam = _serializer.SerializeJobParam(command),
            JobName = jobName,
            Cron = cron,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
            ScheduledStartAt = opts.StartTime ?? CronHelper.GetNext(cron, DateTime.UtcNow),
            CanBeRestarted = command.CanBeRestarted(),
            QueueName = _queueNameAssignor.GetQueueNameForRecurrent(jobName, opts),
            SerializableGroupId = opts.SerializableGroupId,
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
