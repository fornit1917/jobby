using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard.Authorization;

public static class JobbyDashboardBasicAuthExtensions
{
    /// <summary>
    /// Registers only the Basic authentication scheme. Therefore, callers must still configure dashboard authorization.
    /// </summary>
    public static IJobbyDashboardBuilder AddBasicAuthScheme(
        this IJobbyDashboardBuilder builder, Action<JobbyDashboardBasicOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var probe = new JobbyDashboardBasicOptions();
        configure(probe);
        if (string.IsNullOrWhiteSpace(probe.Username))
        {
            throw new InvalidOperationException("AddBasicAuthScheme: Username must be set.");
        }

        if (!PasswordHasher.IsValidEncoding(probe.PasswordHash))
        {
            throw new InvalidOperationException(
                "AddBasicAuthScheme: PasswordHash must be a PBKDF2 hash produced by PasswordHasher.Hash(...). " +
                "Do not store a plaintext password.");
        }

        builder.Services.AddAuthentication()
            .AddScheme<JobbyDashboardBasicOptions, JobbyDashboardBasicHandler>(
                JobbyDashboardBasicDefaults.Scheme, configure);
        return builder;
    }

    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static IJobbyDashboardBuilder AddBasicAuth(
        this IJobbyDashboardBuilder builder, Action<JobbyDashboardBasicOptions> configure)
    {
        return builder
            .AddBasicAuthScheme(configure)
            .RequireAuthorization(read => read
                .AddAuthenticationSchemes(JobbyDashboardBasicDefaults.Scheme)
                .RequireAuthenticatedUser());
    }
}
