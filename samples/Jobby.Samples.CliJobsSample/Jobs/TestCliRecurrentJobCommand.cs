using Jobby.Core.Interfaces;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliRecurrentJobCommand : IJobCommand
{
    public string? Value { get; set; }
    
    public static string GetJobName() => "TestRecurrentJob";
}
