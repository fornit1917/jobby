using Jobby.Core.Interfaces;

namespace Jobby.TestsUtils.Jobs;

public class TestNoAsyncJobCommand : IJobCommand
{
    public Exception? ExceptionToThrow { get; init; }

    public static string GetJobName() => "TestNoAsyncJob";
    public bool CanBeRestarted() => true;
}
