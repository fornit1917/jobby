using Jobby.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.AspNetCore;

internal class AspNetCoreJobExecutionScope : IJobExecutionScope
{
    private readonly IServiceScope _containerScope;

    public AspNetCoreJobExecutionScope(IServiceScope containerScope)
    {
        _containerScope = containerScope;
    }

    public object? GetService(Type type)
    {
        return _containerScope.ServiceProvider.GetService(type);
    }

    public void Dispose()
    {
        _containerScope.Dispose();
    }
}
