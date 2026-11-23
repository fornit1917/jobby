using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Jobby.Tests.Dashboard.Components;

public class CreateJobDialogTests : DashboardClientTestContext
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly Guid _createdId = Guid.NewGuid();
    private IRenderedComponent<MudDialogProvider> _provider = null!;
    private IDialogReference _dialog = null!;

    // Request bodies are read while the request is being sent: the client disposes them afterwards.
    private readonly List<string> _bodies = [];

    private async Task ShowAsync(bool recurrent, Func<object>? respond = null)
    {
        await SetupApiAndLoadConfigAsync(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/config")
            {
                return new JobbyDashboardClientConfigDto
                {
                    ManagementEnabled = true,
                    CreatableJobNames = ["SendReport"],
                    ManagementRequestHeaderName = "X-Jobby-Dashboard",
                    ManagementRequestToken = "tok",
                };
            }

            _bodies.Add(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return respond?.Invoke() ?? new CreateJobResultDto(_createdId);
        });

        _provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<CreateJobDialog> { { dialog => dialog.Recurrent, recurrent } };
        await _provider.InvokeAsync(async () =>
            _dialog = await Services.GetRequiredService<IDialogService>()
                .ShowAsync<CreateJobDialog>("Create job", parameters));
    }

    private bool SubmitDisabled => _provider.Find("[data-testid=create-job-submit]").HasAttribute("disabled");

    [Fact]
    public async Task Submit_PostsTheOnlyCreatableJobWithItsParametersAndClosesWithTheId()
    {
        await ShowAsync(recurrent: false);
        _provider.Find("[data-testid=create-job-param]").Input("""{"Recipient":"ops@example.com"}""");
        _provider.Find("[data-testid=create-job-queue]").Change("urgent");

        await _provider.Find("[data-testid=create-job-submit]").ClickAsync();

        var result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.False(result!.Canceled);
        Assert.Equal(_createdId, result.Data);
        var request = Assert.Single(Handler.RequestsTo("/api/jobs"));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("tok", Assert.Single(request.Headers.GetValues("X-Jobby-Dashboard")));
        Assert.Equal(
            new CreateJobRequest(JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com"}""",
                QueueName: "urgent"),
            JsonSerializer.Deserialize<CreateJobRequest>(Assert.Single(_bodies), WebJson));
    }

    [Fact]
    public async Task Submit_Recurrent_PostsCronAndExclusiveFlag()
    {
        await ShowAsync(recurrent: true);
        _provider.Find("[data-testid=create-job-cron]").Input(" 0 6 * * * ");
        _provider.Find("[data-testid=create-job-exclusive]").Change(false);

        await _provider.Find("[data-testid=create-job-submit]").ClickAsync();

        await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Single(Handler.RequestsTo("/api/recurrent"));
        Assert.Equal(
            new CreateRecurrentJobRequest(JobName: "SendReport", JobParam: "{}", Cron: "0 6 * * *",
                QueueName: null, IsExclusive: false),
            JsonSerializer.Deserialize<CreateRecurrentJobRequest>(Assert.Single(_bodies), WebJson));
    }

    [Fact]
    public async Task RejectedValues_ShowTheServerReasonAndKeepTheDialogOpen()
    {
        await ShowAsync(recurrent: false, respond: () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new DashboardErrorDto("Parameters do not match job 'SendReport': oops")),
        });

        await _provider.Find("[data-testid=create-job-submit]").ClickAsync();

        _provider.WaitForAssertion(() => Assert.Equal(
            "Parameters do not match job 'SendReport': oops",
            _provider.Find("[data-testid=create-job-error]").TextContent.Trim()));
        Assert.False(_dialog.Result.IsCompleted);
        Assert.False(SubmitDisabled);
    }

    [Fact]
    public async Task Submit_IsDisabledUntilParametersAndCronAreFilled()
    {
        await ShowAsync(recurrent: true);
        Assert.True(SubmitDisabled);

        _provider.Find("[data-testid=create-job-cron]").Input("0 6 * * *");
        Assert.False(SubmitDisabled);

        _provider.Find("[data-testid=create-job-param]").Input("  ");
        Assert.True(SubmitDisabled);
    }
}
