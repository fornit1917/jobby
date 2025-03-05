namespace Jobby.Core.Interfaces.Builders;

public interface IJobbyServicesBuilder
{
    IJobsClient CreateJobsClient();
    IRecurrentJobsClient CreateRecurrentJobsClient();
    IJobbyServer CreateJobbyServer();
}
