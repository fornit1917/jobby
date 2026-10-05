using Bunit;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard.Components;

public class StackedStatusBarTests : MudTestContext
{
    [Fact]
    public void Renders_OneSegmentPerNonzeroCountSizedByProportion()
    {
        IReadOnlyList<DashboardStatusCount> counts =
        [
            new(JobStatus.Scheduled, 3),
            new(JobStatus.Failed, 1),
            new(JobStatus.Completed, 0),
        ];

        var cut = Render<StackedStatusBar>(p => p.Add(c => c.Counts, counts));

        var segments = cut.FindAll(".jobby-stack-bar > i");
        Assert.Equal(2, segments.Count);
        Assert.Contains("jobby-seg-Scheduled", segments[0].ClassName);
        Assert.Contains("width:75%", segments[0].GetAttribute("style")!.Replace(" ", ""));
        Assert.Contains("jobby-seg-Failed", segments[1].ClassName);
        Assert.Contains("width:25%", segments[1].GetAttribute("style")!.Replace(" ", ""));
    }

    [Fact]
    public void Empty_OrAllZeroRendersAnEmptyTrack()
    {
        var cut = Render<StackedStatusBar>(p => p.Add(c => c.Counts, (IReadOnlyList<DashboardStatusCount>)[]));
        Assert.NotEmpty(cut.FindAll(".jobby-stack-bar"));
        Assert.Empty(cut.FindAll(".jobby-stack-bar > i"));
    }
}