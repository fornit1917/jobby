using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Moq;

namespace Jobby.TestsUtils.Mocks;

public static class JobbyStorageMockTakeToProcessingExtensions
{
    public static void SetupTakeToProcessing(this Mock<IJobbyStorage> mock, GetJobsRequest request,
        List<JobExecutionModel> result)
    {
        mock
            .Setup(x => x.TakeBatchToProcessingAsync(request, It.IsAny<List<JobExecutionModel>>()))
            .Callback<GetJobsRequest, List<JobExecutionModel>>((_, output) =>
            {
                output.Clear();
                output.AddRange(result);
            });
    }

    public static void VerifyTakeToProcessing(this Mock<IJobbyStorage> mock, GetJobsRequest request)
    {
        mock .Verify(x => x.TakeBatchToProcessingAsync(request,
                It.IsAny<List<JobExecutionModel>>()), Times.Once);
    }
    
    public static void VerifyTakeToProcessing(this Mock<IJobbyStorage> mock, GetJobsRequest request,
        Times expectedTimes)
    {
        mock .Verify(x => x.TakeBatchToProcessingAsync(request,
            It.IsAny<List<JobExecutionModel>>()), expectedTimes);
    }
}