using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Text.Json.Serialization;

namespace Jobby.Samples.AspNet.Jobs
{
    public class DemoJobCommand : IJobCommand, IHasDefaultJobOptions
    {
        public string Value { get; init; } = "SomeValue";
        public bool ShouldBeFailed { get; init; } = false;
        public bool ShouldThrowIgnoredException { get; init; } = false;
        public DateTime? StartAfter { get; init; }
        public int DelayMs { get; init; }
        public string? QueueName { get; init; }
        public string? SerializableGroupId { get; init; }
        public bool LockGroupIfFailed { get; init; } = false;

        // accept enum names ("RestoreTraceId") in JSON bodies, not only numbers
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JobbyTracingMode? TracingMode { get; init; }

        public static string GetJobName() => "DemoJob";

        public JobOpts GetOptionsForEnqueuedJob() => new JobOpts
        {
            StartTime = StartAfter,
            QueueName = QueueName,
            SerializableGroupId = SerializableGroupId,
            LockGroupIfFailed = LockGroupIfFailed,
            TracingMode = TracingMode,
        };

        public RecurrentJobOpts GetOptionsForRecurrentJob() => default(RecurrentJobOpts);
    }
}
