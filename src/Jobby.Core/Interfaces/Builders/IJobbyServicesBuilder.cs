namespace Jobby.Core.Interfaces.Builders;

public interface IJobbyServicesBuilder
{
    IJobsClient CreateJobsClient();
    IJobbyServer CreateJobbyServer();
}
