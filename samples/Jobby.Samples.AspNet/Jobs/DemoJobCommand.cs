using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.Jobs
{
    public class DemoJobCommand : IJobCommand, IHasDefaultJobOptions
    {
        public string Value { get; init; } = "SomeValue";
        public bool ShouldBeFailed { get; init; } = false;
        public bool ShouldThrowIgnoredException { get; init; } = false;
        public DateTime? StartAfter { get; init; }
        public int DelayMs { get; init; }
        public string? SerializableGroupId { get; init; }
        public bool LockGroupIfFailed { get; init; } = false;

        public static string GetJobName() => "DemoJob";

        public JobOpts GetOptionsForEnqueuedJob() => new JobOpts
        {
            StartTime = StartAfter,
            SerializableGroupId = SerializableGroupId,
            LockGroupIfFailed = LockGroupIfFailed,
        };

        public RecurrentJobOpts GetOptionsForRecurrentJob() => default(RecurrentJobOpts);
    }
}
