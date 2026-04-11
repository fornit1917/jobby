using Jobby.Core.Interfaces;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommand : IJobCommand
{
    public Guid UniqueId { get; init; } = Guid.NewGuid();
    public Exception? ExceptionToThrow { get; init; }

    public static string GetJobName() => "TestJob";
}
