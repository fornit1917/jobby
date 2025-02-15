using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Core.Client;
using Moq;

namespace Jobby.Core.Tests.Client;

public class JobsClientTests
{
    private readonly Mock<IJobsStorage> _jobsStorageMock;
    private readonly Mock<IJobParamSerializer> _serializerMock;
    private readonly JobsClient _jobsClient;

    public JobsClientTests()
    {
        _jobsStorageMock = new Mock<IJobsStorage>();
        _serializerMock = new Mock<IJobParamSerializer>();
        _jobsClient = new JobsClient(_jobsStorageMock.Object, _serializerMock.Object);
    }

    [Fact]
    public async Task EnqueueAsync_SetsAndReturnsJobIdFromStorage()
    {
        var job = new JobModel();
        long expectedJobId = 123;
        _jobsStorageMock
            .Setup(x => x.InsertAsync(job))
            .ReturnsAsync(expectedJobId);


        var actualJobId = await _jobsClient.EnqueueAsync(job);

        Assert.Equal(expectedJobId, actualJobId);
        Assert.Equal(expectedJobId, job.Id);
    }

    [Fact]
    public async Task EnqueueAsync_SetsCreatedAt()
    {
        var job = new JobModel();

        await _jobsClient.EnqueueAsync(job);

        var now = DateTime.UtcNow;
        Assert.True(now.Subtract(job.CreatedAt) < TimeSpan.FromSeconds(1));
    }
}
