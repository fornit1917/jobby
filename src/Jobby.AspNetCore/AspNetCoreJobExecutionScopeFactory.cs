using Jobby.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.AspNetCore;

internal class AspNetCoreJobExecutionScopeFactory : IJobExecutionScopeFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AspNetCoreJobExecutionScopeFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJobExecutionScope CreateJobExecutionScope()
    {
        _serviceProvider.CreateAsyncScope();
        return new AspNetCoreJobExecutionScope(_serviceProvider.CreateScope());
    }
}
