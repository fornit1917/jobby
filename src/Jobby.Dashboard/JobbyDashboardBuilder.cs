using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard;

public interface IJobbyDashboardBuilder
{
    IServiceCollection Services { get; }
}

internal sealed class JobbyDashboardBuilder : IJobbyDashboardBuilder
{
    public IServiceCollection Services { get; }
    public JobbyDashboardAuthState AuthState { get; }

    public JobbyDashboardBuilder(IServiceCollection services, JobbyDashboardAuthState authState)
    {
        Services = services;
        AuthState = authState;
    }
}
