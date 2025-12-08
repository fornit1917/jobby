using Jobby.Core.Interfaces;

namespace Jobby.Samples.AspNet.Jobs
{
    public class DemoJobCommand : IJobCommand
    {
        public bool ShouldBeFailed { get; init; } = false;
        public bool ShouldThrowIgnoredException { get; init; } = false;
        public DateTime? StartAfter { get; init; }
        public int DelayMs { get; init; }

        public static string GetJobName() => "DemoJob";
        public bool CanBeRestarted() => true;
    }
}
