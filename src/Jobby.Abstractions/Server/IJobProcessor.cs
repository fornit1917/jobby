using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Server;

public interface IJobProcessor
{
    Task LockProcessingSlot();
    int GetFreeProcessingSlotsCount();
    void ReleaseProcessingSlot();

    void StartProcessing(JobModel job);
    void StartProcessing(IReadOnlyList<JobModel> jobs);
}
