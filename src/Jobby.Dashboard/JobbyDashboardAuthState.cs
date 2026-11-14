namespace Jobby.Dashboard;

internal enum JobbyDashboardAuthMode
{
    Unset,
    Anonymous,
    Policy
}

internal sealed class JobbyDashboardAuthState
{
    public JobbyDashboardAuthMode Mode { get; private set; } = JobbyDashboardAuthMode.Unset;
    public string ReadPolicy { get; private set; } = JobbyDashboardPolicies.Read;
    public string ManagePolicy { get; private set; } = JobbyDashboardPolicies.Manage;

    public void Configure(JobbyDashboardAuthMode mode, string? readPolicy = null, string? managePolicy = null)
    {
        if (Mode != JobbyDashboardAuthMode.Unset)
        {
            throw new InvalidOperationException(
                $"Jobby Dashboard auth is already configured ({Mode}). Configure exactly one auth model.");
        }

        Mode = mode;

        if (readPolicy is not null)
        {
            ReadPolicy = readPolicy;
        }

        ManagePolicy = managePolicy ?? readPolicy ?? ManagePolicy;
    }
}