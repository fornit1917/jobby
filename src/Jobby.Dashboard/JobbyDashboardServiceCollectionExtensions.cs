using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard;

public static class JobbyDashboardServiceCollectionExtensions
{
    public static IJobbyDashboardBuilder AddJobbyDashboard(
        this IServiceCollection services,
        Action<JobbyDashboardOptions>? configure = null)
    {
        if (services.Any(d => d.ServiceType == typeof(JobbyDashboardAuthState)))
        {
            throw new InvalidOperationException(
                "AddJobbyDashboard has already been called; call it exactly once per application.");
        }

        var options = new JobbyDashboardOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        var authState = new JobbyDashboardAuthState();
        services.AddSingleton(authState);

        services.AddAuthentication();
        services.AddAuthorization();

        services.AddAntiforgery();
        services.AddScoped<IDashboardDataReader, DashboardDataReader>();

        return new JobbyDashboardBuilder(services, authState);
    }
}