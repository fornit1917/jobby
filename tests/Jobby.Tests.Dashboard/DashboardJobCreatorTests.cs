using System.Text.Json;
using System.Text.Json.Serialization;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Core.Services;
using Jobby.Dashboard;

namespace Jobby.Tests.Dashboard;

public class DashboardJobCreatorTests
{
    private readonly InMemoryJobbyClient _client;
    private readonly DashboardJobCreator _creator;

    public DashboardJobCreatorTests()
    {
        var builder = new JobbyBuilder();
        builder.AddJob<SendReportCommand, SendReportHandler>();
        _client = new InMemoryJobbyClient(builder.CreateJobsFactory());
        _creator = new DashboardJobCreator([builder], [_client]);
    }

    [Fact]
    public void JobNames_ListJobsRegisteredInTheHost()
    {
        Assert.Equal(["SendReport"], _creator.JobNames);
    }

    [Fact]
    public void JobNames_AreEmptyWhenTheHostHasNoJobbyClient()
    {
        var builder = new JobbyBuilder();
        builder.AddJob<SendReportCommand, SendReportHandler>();

        Assert.Empty(new DashboardJobCreator([builder], []).JobNames);
        Assert.Empty(new DashboardJobCreator([], [_client]).JobNames);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesTheJobFromItsParameters()
    {
        var id = await _creator.EnqueueAsync(new CreateJobRequest(
            JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com","Copies":3}""", QueueName: null));

        var job = Assert.Single(_client.Jobs);
        Assert.Equal(id, job.Id);
        Assert.Equal("SendReport", job.JobName);
        Assert.Equal("""{"Recipient":"ops@example.com","Copies":3}""", job.JobParam);
        Assert.Null(job.Schedule);
        // No queue was submitted, so the job keeps the default queue of its command.
        Assert.Equal("reports", job.QueueName);
    }

    [Theory]
    [InlineData("urgent", "urgent")]
    [InlineData(" urgent ", "urgent")]
    [InlineData("  ", "reports")]
    public async Task EnqueueAsync_SubmittedQueueIsTrimmedAndReplacesTheDefaultUnlessBlank(string queueName,
        string expected)
    {
        await _creator.EnqueueAsync(new CreateJobRequest(
            JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com"}""", QueueName: queueName));

        Assert.Equal(expected, Assert.Single(_client.Jobs).QueueName);
    }

    [Theory]
    [InlineData("""{"recipient":"ops@example.com","copies":3}""")]
    [InlineData("""{"RECIPIENT":"ops@example.com","COPIES":3}""")]
    public async Task EnqueueAsync_ParameterNamesInAnotherCase_AreReadAndStoredTheWayTheSerializerWritesThem(
        string jobParam)
    {
        await _creator.EnqueueAsync(new CreateJobRequest(JobName: "SendReport", JobParam: jobParam, QueueName: null));

        Assert.Equal("""{"Recipient":"ops@example.com","Copies":3}""", Assert.Single(_client.Jobs).JobParam);
    }

    [Fact]
    public async Task EnqueueAsync_JsonOptionsOfTheApplication_AreAppliedAsTheyAre()
    {
        var builder = new JobbyBuilder();
        builder.AddJob<SendReportCommand, SendReportHandler>();
        builder.UseSystemTextJson(new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        });
        var client = new InMemoryJobbyClient(builder.CreateJobsFactory());
        var creator = new DashboardJobCreator([builder], [client]);

        // The number is a string, which only the options of the application accept.
        await creator.EnqueueAsync(new CreateJobRequest(
            JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com","Copies":"3"}""", QueueName: null));

        Assert.Equal("""{"Recipient":"ops@example.com","Copies":3}""", Assert.Single(client.Jobs).JobParam);

        // These options match names exactly, and the dashboard does not relax what the application chose.
        var error = await Assert.ThrowsAsync<DashboardJobCreationException>(() =>
            creator.EnqueueAsync(new CreateJobRequest(
                JobName: "SendReport", JobParam: """{"recipient":"ops@example.com"}""", QueueName: null)));

        Assert.StartsWith("Parameters do not match job 'SendReport': ", error.Message);
        Assert.Single(client.Jobs);
    }

    [Fact]
    public async Task EnqueueAsync_SerializerOfTheApplication_ReadsTypedParametersItself()
    {
        var builder = new JobbyBuilder();
        builder.AddJob<SendReportCommand, SendReportHandler>();
        builder.UseSerializer(new RecipientTextSerializer());
        var client = new InMemoryJobbyClient(builder.CreateJobsFactory());
        var creator = new DashboardJobCreator([builder], [client]);

        await creator.EnqueueAsync(new CreateJobRequest(
            JobName: "SendReport", JobParam: "ops@example.com", QueueName: null));

        Assert.Equal("ops@example.com", Assert.Single(client.Jobs).JobParam);
    }

    [Fact]
    public async Task EnqueueAsync_UnknownJob_IsRejected()
    {
        var error = await Assert.ThrowsAsync<DashboardJobCreationException>(() =>
            _creator.EnqueueAsync(new CreateJobRequest(JobName: "Missing", JobParam: "{}", QueueName: null)));

        Assert.Equal("Job 'Missing' is not registered in this application.", error.Message);
        Assert.Empty(_client.Jobs);
    }

    [Theory]
    // not JSON at all
    [InlineData("{oops")]
    // valid JSON of the wrong shape: the mandatory Recipient is missing
    [InlineData("{}")]
    // a property of the wrong type
    [InlineData("""{"Recipient":"ops@example.com","Copies":"many"}""")]
    public async Task EnqueueAsync_ParametersThatDoNotMatchTheCommand_AreRejected(string jobParam)
    {
        var error = await Assert.ThrowsAsync<DashboardJobCreationException>(() =>
            _creator.EnqueueAsync(new CreateJobRequest(JobName: "SendReport", JobParam: jobParam, QueueName: null)));

        Assert.StartsWith("Parameters do not match job 'SendReport': ", error.Message);
        Assert.Empty(_client.Jobs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task EnqueueAsync_MissingParameters_AreRejected(string? jobParam)
    {
        var error = await Assert.ThrowsAsync<DashboardJobCreationException>(() =>
            _creator.EnqueueAsync(new CreateJobRequest(JobName: "SendReport", JobParam: jobParam, QueueName: null)));

        Assert.Equal("Parameters of job 'SendReport' are required.", error.Message);
        Assert.Empty(_client.Jobs);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScheduleRecurrentAsync_CreatesCronJob(bool isExclusive)
    {
        var id = await _creator.ScheduleRecurrentAsync(new CreateRecurrentJobRequest(
            JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com"}""", Cron: "0 6 * * *",
            QueueName: null, IsExclusive: isExclusive));

        var job = Assert.Single(_client.Jobs);
        Assert.Equal(id, job.Id);
        Assert.Equal("SendReport", job.JobName);
        Assert.Equal("0 6 * * *", job.Schedule);
        Assert.Equal(isExclusive, job.IsExclusive);
        Assert.Equal("recurrent-reports", job.QueueName);
        Assert.Equal(JobStatus.Scheduled, job.Status);
    }

    [Theory]
    [InlineData("every morning")]
    [InlineData("")]
    public async Task ScheduleRecurrentAsync_InvalidCron_IsRejected(string cron)
    {
        var error = await Assert.ThrowsAsync<DashboardJobCreationException>(() =>
            _creator.ScheduleRecurrentAsync(new CreateRecurrentJobRequest(
                JobName: "SendReport", JobParam: """{"Recipient":"ops@example.com"}""", Cron: cron,
                QueueName: null, IsExclusive: true)));

        Assert.Equal($"Could not parse cron expression '{cron}'", error.Message);
        Assert.Empty(_client.Jobs);
    }
}
