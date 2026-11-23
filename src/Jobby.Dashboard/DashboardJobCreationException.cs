namespace Jobby.Dashboard;

/// <summary>
/// A job could not be created from the dashboard because of the submitted values: an unknown job name,
/// parameters that do not match the job command, or an invalid schedule. The message is shown to the user.
/// </summary>
public sealed class DashboardJobCreationException : Exception
{
    public DashboardJobCreationException(string message) : base(message)
    {
    }
}
