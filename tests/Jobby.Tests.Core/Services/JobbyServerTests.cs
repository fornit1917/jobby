using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jobby.Tests.Core.Services;

public class JobbyServerTests
{
    private readonly Mock<IAvailabilityCheckServerModule> _availabilityCheckServerModuleMock;
    private readonly Mock<IJobsExecutionServerModule> _jobsExecutionServerModuleMock;
    private readonly Mock<IPermanentLocksCheckServerModule> _permanentLocksCheckServerModuleMock;
    private readonly Mock<ILogger<JobbyServer>> _loggerMock;

    private readonly JobbyServer _server;

    private const string ServerId = "serverId";

    public JobbyServerTests()
    {
        _availabilityCheckServerModuleMock = new Mock<IAvailabilityCheckServerModule>();
        _jobsExecutionServerModuleMock = new Mock<IJobsExecutionServerModule>();
        _permanentLocksCheckServerModuleMock = new Mock<IPermanentLocksCheckServerModule>();
        _loggerMock = new Mock<ILogger<JobbyServer>>();

        _server = new JobbyServer(_availabilityCheckServerModuleMock.Object,
            _jobsExecutionServerModuleMock.Object,
            _permanentLocksCheckServerModuleMock.Object,
            _loggerMock.Object, ServerId);
    }

    [Fact]
    public void StartBackgroundService_StartsAllModules()
    {
        _server.StartBackgroundService();
        
        _availabilityCheckServerModuleMock.Verify(x => x.Start(), Times.Once);
        _jobsExecutionServerModuleMock.Verify(x => x.Start(), Times.Once);
        _permanentLocksCheckServerModuleMock.Verify(x => x.Start(), Times.Once);
    }

    [Fact]
    public void SendStopSignal_SendsStopSignalToAllModules()
    {
        _server.SendStopSignal();
        
        _availabilityCheckServerModuleMock.Verify(x => x.SendStopSignal(), Times.Once);
        _jobsExecutionServerModuleMock.Verify(x => x.SendStopSignal(), Times.Once);
        _permanentLocksCheckServerModuleMock.Verify(x => x.SendStopSignal(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasInProgressJobs_ReturnsHasInProgressJobsFromJobsExecutionModule(bool hasInProgressJobs)
    {
        _jobsExecutionServerModuleMock.Setup(x => x.HasInProgressJobs()).Returns(hasInProgressJobs);
        
        Assert.Equal(hasInProgressJobs, _server.HasInProgressJobs());
    }
}
