using Microsoft.AspNetCore.Authentication;

namespace Jobby.Dashboard.Authorization;

public sealed class JobbyDashboardBasicOptions : AuthenticationSchemeOptions
{
    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string Realm { get; set; } = "Jobby Dashboard";
}