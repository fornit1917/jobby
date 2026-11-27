using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Tests.Dashboard;

// A job with a mandatory parameter and its own default queues, for creating jobs from the dashboard.
internal sealed class SendReportCommand : IJobCommand, IHasDefaultJobOptions
{
    public required string Recipient { get; init; }

    public int Copies { get; init; } = 1;

    public static string GetJobName() => "SendReport";

    public JobOpts GetOptionsForEnqueuedJob() => new() { QueueName = "reports" };

    public RecurrentJobOpts GetOptionsForRecurrentJob() => new() { QueueName = "recurrent-reports" };
}

internal sealed class SendReportHandler : IJobCommandHandler<SendReportCommand>
{
    public Task ExecuteAsync(SendReportCommand command, JobExecutionContext ctx) => Task.CompletedTask;
}
