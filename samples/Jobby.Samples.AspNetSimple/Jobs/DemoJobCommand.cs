using Jobby.Core.Interfaces;

namespace Jobby.Samples.AspNetSimple.Jobs
{
    public class DemoJobCommand : IJobCommand
    {
        public bool ShouldBeFailed { get; init; }
        public DateTime? StartAfter { get; init; }
        public int DelayMs { get; init; }

        public static string GetJobName() => "DemoJob";
        public bool CanBeRestarted() => true;
    }
}
