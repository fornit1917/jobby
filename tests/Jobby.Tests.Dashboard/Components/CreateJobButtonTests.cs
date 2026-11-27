using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI;
using Jobby.Dashboard.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class CreateJobButtonTests : DashboardClientTestContext
{
    private Task SetupAsync(params string[] creatableJobNames) =>
        SetupApiAndLoadConfigAsync(req => req.RequestUri!.AbsolutePath == "/api/config"
            ? new JobbyDashboardClientConfigDto
            {
                ManagementEnabled = true,
                CreatableJobNames = creatableJobNames,
                ManagementRequestHeaderName = "X-Jobby-Dashboard",
                ManagementRequestToken = "tok",
            }
            : new CreateJobResultDto(Guid.NewGuid()));

    // Opening is not awaited: the click handler of the button completes only when the dialog closes.
    private static void OpenDialog(IRenderedComponent<Bunit.Rendering.ContainerFragment> cut)
    {
        cut.Find("[data-testid=create-job]").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=create-job-form]")));
    }

    [Fact]
    public async Task Hidden_WhenNoJobCanBeCreated()
    {
        await SetupAsync();

        var cut = Render<CreateJobButton>();

        Assert.Empty(cut.FindAll("[data-testid=create-job]"));
    }

    [Fact]
    public async Task Hidden_InReadOnlyView_AndBackWhenManagementViewReturns()
    {
        await SetupAsync("SendReport");
        var mode = Services.GetRequiredService<DashboardModeState>();
        var cut = Render<CreateJobButton>();
        Assert.NotEmpty(cut.FindAll("[data-testid=create-job]"));

        mode.Mode = DashboardMode.ReadOnly;
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=create-job]")));

        mode.Mode = DashboardMode.Management;
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=create-job]")));
    }

    [Theory]
    [InlineData(false, "Create job", "/api/jobs")]
    [InlineData(true, "Create recurrent job", "/api/recurrent")]
    public async Task Click_OpensTheDialog_AndACreatedJobIsReportedOnce(bool recurrent, string title, string endpoint)
    {
        await SetupAsync("SendReport");
        var created = 0;
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<CreateJobButton>(1);
            builder.AddAttribute(2, nameof(CreateJobButton.Recurrent), recurrent);
            builder.AddAttribute(3, nameof(CreateJobButton.OnCreated),
                EventCallback.Factory.Create(this, () => created++));
            builder.CloseComponent();
        });
        Assert.Equal(title, cut.Find("[data-testid=create-job]").TextContent.Trim());

        OpenDialog(cut);
        Assert.Equal(title, cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(0, created);

        if (recurrent)
        {
            cut.Find("[data-testid=create-job-cron]").Input("0 6 * * *");
        }

        await cut.Find("[data-testid=create-job-submit]").ClickAsync();

        cut.WaitForAssertion(() => Assert.Equal(1, created));
        Assert.Single(Handler.RequestsTo(endpoint));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=create-job-form]")));
    }

    [Fact]
    public async Task Cancel_ClosesTheDialogWithoutCreatingOrReporting()
    {
        await SetupAsync("SendReport");
        var created = 0;
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<CreateJobButton>(1);
            builder.AddAttribute(2, nameof(CreateJobButton.OnCreated),
                EventCallback.Factory.Create(this, () => created++));
            builder.CloseComponent();
        });
        OpenDialog(cut);

        await cut.Find("[data-testid=create-job-cancel]").ClickAsync();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=create-job-form]")));
        Assert.Equal(0, created);
        Assert.Empty(Handler.RequestsTo("/api/jobs"));
    }
}
