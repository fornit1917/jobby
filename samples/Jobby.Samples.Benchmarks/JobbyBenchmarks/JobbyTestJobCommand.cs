using Jobby.Abstractions.Models;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public class JobbyTestJobCommand : IJobCommand
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DelayMs { get; set; }

    public static string GetJobName() => "TestJob";
}

public class JobbyTestJobCommandHandler : IJobCommandHandler<JobbyTestJobCommand>
{
    public async Task ExecuteAsync(JobbyTestJobCommand command)
    {
        if (command?.DelayMs > 0)
        {
            await Task.Delay(command.DelayMs);
        }
    }
}