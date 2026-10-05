using Jobby.Dashboard;

namespace Jobby.Tests.Dashboard;

public class JobbyDashboardAuthStateTests
{
    [Fact]
    public void Default_IsUnset_WithDefaultPolicyNames()
    {
        var s = new JobbyDashboardAuthState();
        Assert.Equal(JobbyDashboardAuthMode.Unset, s.Mode);
        Assert.Equal(JobbyDashboardPolicies.Read, s.ReadPolicy);
        Assert.Equal(JobbyDashboardPolicies.Manage, s.ManagePolicy);
    }

    [Fact]
    public void Configure_Policy_SetsModeAndPolicies()
    {
        var s = new JobbyDashboardAuthState();
        s.Configure(JobbyDashboardAuthMode.Policy, "R", "M");
        Assert.Equal(JobbyDashboardAuthMode.Policy, s.Mode);
        Assert.Equal("R", s.ReadPolicy);
        Assert.Equal("M", s.ManagePolicy);
    }

    [Fact]
    public void Configure_ManageFallsBackToRead()
    {
        var s = new JobbyDashboardAuthState();
        s.Configure(JobbyDashboardAuthMode.Policy, "R");
        Assert.Equal("R", s.ManagePolicy);
    }

    [Fact]
    public void Configure_SecondDecision_Throws()
    {
        var s = new JobbyDashboardAuthState();
        s.Configure(JobbyDashboardAuthMode.Anonymous);
        Assert.Throws<InvalidOperationException>(() => s.Configure(JobbyDashboardAuthMode.Policy));
    }
}