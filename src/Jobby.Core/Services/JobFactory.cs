﻿using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class JobFactory : IJobFactory
{
    private readonly IJobParamSerializer _serializer;

    public JobFactory(IJobParamSerializer serializer)
    {
        _serializer = serializer;
    }

    public JobModel Create<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        return new JobModel
        {
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = DateTime.UtcNow,
            Status = JobStatus.Scheduled,
        };
    }

    public JobModel Create<TCommand>(TCommand command, DateTime startTime) where TCommand : IJobCommand
    {
        return new JobModel
        {
            CreatedAt = DateTime.UtcNow,
            JobName = TCommand.GetJobName(),
            JobParam = _serializer.SerializeJobParam(command),
            ScheduledStartAt = startTime,
            Status = JobStatus.Scheduled,
        };
    }
}
