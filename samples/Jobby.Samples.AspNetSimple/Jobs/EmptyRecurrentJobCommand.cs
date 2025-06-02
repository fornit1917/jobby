using Jobby.Core.Interfaces;

namespace Jobby.Samples.AspNetSimple.Jobs;

public class EmptyRecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "EmptyRecurrentJob";
    public bool CanBeRestarted() => true;
}
