using Bunit;
using Jobby.Core.Models;
using Jobby.Dashboard.Client.Shared;

namespace Jobby.Tests.Dashboard.Components;

public class StatusChipTests : MudTestContext
{
    [Theory]
    [InlineData(JobStatus.Scheduled, "jobby-tag-Scheduled")]
    [InlineData(JobStatus.Processing, "jobby-tag-Processing")]
    [InlineData(JobStatus.Completed, "jobby-tag-Completed")]
    [InlineData(JobStatus.Failed, "jobby-tag-Failed")]
    [InlineData(JobStatus.Frozen, "jobby-tag-Frozen")]
    [InlineData(JobStatus.WaitingPrev, "jobby-tag-WaitingPrev")]
    public void Renders_StatusTagWithClass(JobStatus status, string cssClass)
    {
        var cut = Render<StatusChip>(p => p.Add(c => c.Status, status));

        var el = cut.Find("[data-testid=status-chip]");
        Assert.Contains("jobby-tag", el.ClassName);
        Assert.Contains(cssClass, el.ClassName);
        Assert.Equal(status.ToString(), el.TextContent.Trim());
    }
}
