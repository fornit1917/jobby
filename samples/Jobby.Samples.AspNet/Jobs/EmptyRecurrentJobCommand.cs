using Jobby.Core.Interfaces;

namespace Jobby.Samples.AspNet.Jobs;

public class EmptyRecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "EmptyRecurrentJob";
}
