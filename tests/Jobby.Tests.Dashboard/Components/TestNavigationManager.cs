using Microsoft.AspNetCore.Components;

namespace Jobby.Tests.Dashboard.Components;

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
    }
}