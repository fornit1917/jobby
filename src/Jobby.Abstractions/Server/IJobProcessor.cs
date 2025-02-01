using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Server;

public interface IJobProcessor
{
    Task LockProcessingSlot();
    void StartProcessing(JobModel job);
    void ReleaseProcessingSlot();
}
