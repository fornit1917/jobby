using Jobby.Core.Interfaces;

namespace Jobby.Samples.CliJobsSample.Jobs;

internal class TestCliRecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "TestRecurrentJob";
    public bool CanBeRestarted() => false;
}
