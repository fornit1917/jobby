﻿using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.Jobs;

public class EmptyRecurrentJobCommandHandler : IJobCommandHandler<EmptyRecurrentJobCommand>
{
    private readonly ILogger<EmptyRecurrentJobCommandHandler> _logger;

    public EmptyRecurrentJobCommandHandler(ILogger<EmptyRecurrentJobCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(EmptyRecurrentJobCommand command, JobExecutionContext ctx)
    {
        _logger.LogInformation("EmptyRecurrentJob executed, {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
