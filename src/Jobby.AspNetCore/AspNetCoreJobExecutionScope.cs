using Jobby.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.AspNetCore;

internal class AspNetCoreJobExecutionScope : IJobExecutionScope
{
    private readonly IServiceScope _diScope;

    public AspNetCoreJobExecutionScope(IServiceScope diScope)
    {
        _diScope = diScope;
    }

    public object? GetService(Type type)
    {
        return _diScope.ServiceProvider.GetService(type);
    }

    public void Dispose()
    {
        _diScope.Dispose();
    }
}
