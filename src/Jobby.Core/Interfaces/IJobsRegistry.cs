using Jobby.Core.Models;
using System.Reflection;

namespace Jobby.Core.Interfaces;

public interface IJobsRegistry
{
    JobExecutionMetadata? GetJobExecutionMetadata(string jobName);
    RecurrentJobExecutionMetadata? GetRecurrentJobExecutionMetadata(string jobName);
}
