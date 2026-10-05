using Bunit;
using Jobby.Dashboard.Client.Shared;
using Microsoft.AspNetCore.Components;

namespace Jobby.Tests.Dashboard.Components;

public class PageHeaderTests : MudTestContext
{
    [Fact]
    public void Renders_TitleSubtitleAndActions()
    {
        var cut = Render<PageHeader>(p => p
            .Add(c => c.Title, "Jobs")
            .Add(c => c.Subtitle, "11 jobs - 3/4 servers alive")
            .Add(c => c.Actions,
                (RenderFragment)(b => b.AddMarkupContent(0, "<button data-testid=hdr-action>x</button>"))));

        Assert.Contains("Jobs", cut.Find(".jobby-page-title").TextContent);
        Assert.Contains("11 jobs", cut.Find(".jobby-page-sub").TextContent);
        Assert.NotEmpty(cut.FindAll("[data-testid=hdr-action]"));
    }

    [Fact]
    public void Omits_SubtitleNodeWhenNull()
    {
        var cut = Render<PageHeader>(p => p.Add(c => c.Title, "Servers"));
        Assert.Empty(cut.FindAll(".jobby-page-sub"));
    }
}