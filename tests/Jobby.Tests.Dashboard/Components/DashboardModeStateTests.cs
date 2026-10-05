using Jobby.Dashboard.Client;

namespace Jobby.Tests.Dashboard.Components;

public class DashboardModeStateTests
{
    [Fact]
    public void Defaults_ToManagementView()
    {
        var state = new DashboardModeState();
        Assert.Equal(DashboardMode.Management, state.Mode);
        Assert.True(state.IsManagementView);
    }

    [Fact]
    public void Setting_ModeRaisesChangedAndUpdatesFlag()
    {
        var state = new DashboardModeState();
        var raised = 0;
        state.Changed += () => raised++;

        state.Mode = DashboardMode.ReadOnly;

        Assert.Equal(1, raised);
        Assert.False(state.IsManagementView);
    }

    [Fact]
    public void Setting_SameModeDoesNotRaiseChanged()
    {
        var state = new DashboardModeState();
        var raised = 0;
        state.Changed += () => raised++;

        state.Mode = DashboardMode.Management;

        Assert.Equal(0, raised);
    }
}