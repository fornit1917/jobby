using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Jobby.Dashboard;

/// <summary>Note: Exactly one dashboard authorization decision may be made per mount.</summary>
public static class JobbyDashboardBuilderExtensions
{
    /// <summary>Explicit opt-out of authorization for local/dev use.</summary>
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static IJobbyDashboardBuilder AllowAnonymous(this IJobbyDashboardBuilder builder)
    {
        State(builder).Configure(JobbyDashboardAuthMode.Anonymous);
        return builder;
    }

    public static IJobbyDashboardBuilder RequireHostPolicy(
        this IJobbyDashboardBuilder builder, string readPolicy, string? managePolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readPolicy);
        State(builder).Configure(JobbyDashboardAuthMode.Policy, readPolicy, managePolicy);
        return builder;
    }

    public static IJobbyDashboardBuilder RequireAuthorization(
        this IJobbyDashboardBuilder builder,
        Action<AuthorizationPolicyBuilder> readPolicy,
        Action<AuthorizationPolicyBuilder>? managePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(readPolicy);
        // Configure before mutating services so a conflicting second decision fails without partial registration.
        State(builder).Configure(JobbyDashboardAuthMode.Policy);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(JobbyDashboardPolicies.Read, readPolicy)
            .AddPolicy(JobbyDashboardPolicies.Manage, managePolicy ?? readPolicy);
        return builder;
    }

    private static JobbyDashboardAuthState State(IJobbyDashboardBuilder builder)
    {
        return ((JobbyDashboardBuilder)builder).AuthState;
    }
}