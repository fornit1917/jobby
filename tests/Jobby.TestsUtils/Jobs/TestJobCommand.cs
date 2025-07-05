using Jobby.Core.Interfaces;

namespace Jobby.TestsUtils.Jobs;

public class TestJobCommand : IJobCommand
{
    public Guid UniqueId { get; init; } = Guid.NewGuid();
    public bool Restartable { get; init; }
    public Exception? ExceptionToThrow { get; init; }

    public static string GetJobName() => "TestJob";
    public bool CanBeRestarted() => Restartable;
}
