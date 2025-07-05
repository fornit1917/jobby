using Jobby.Core.Interfaces;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliJobCommand : IJobCommand
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool ShouldBeFailed { get; set; }

    public static string GetJobName() => "TestJob";
    public bool CanBeRestarted() => Id % 2 == 0;
}
