using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Moq;

namespace Jobby.TestsUtils.Mocks;

public static class JobbyStorageMockTakeToProcessingExtensions
{
    public static void SetupTakeToProcessing(this Mock<IJobbyStorage> mock, string serverId, int batchSize, string queueName,
        List<JobExecutionModel> result)
    {
        mock
            .Setup(x => x.TakeBatchToProcessingAsync(serverId, batchSize, queueName,
                It.IsAny<List<JobExecutionModel>>()))
            .Callback<string, int, string, List<JobExecutionModel>>((_, _, _, output) =>
            {
                output.Clear();
                output.AddRange(result);
            });
    }

    public static void VerifyTakeToProcessing(this Mock<IJobbyStorage> mock, string serverId, int batchSize, string queueName)
    {
        mock .Verify(x => x.TakeBatchToProcessingAsync(serverId, batchSize, queueName,
                It.IsAny<List<JobExecutionModel>>()), Times.Once);
    }
    
    public static void VerifyTakeToProcessing(this Mock<IJobbyStorage> mock, string serverId, int batchSize, string queueName,
        Times expectedTimes)
    {
        mock .Verify(x => x.TakeBatchToProcessingAsync(serverId, batchSize, queueName,
            It.IsAny<List<JobExecutionModel>>()), expectedTimes);
    }
}