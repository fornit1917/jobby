using Jobby.Core.Models;
using System.Reflection;

namespace Jobby.Core.Interfaces;

internal interface IJobsRegistry
{
    JobExecutionMetadata? GetJobExecutionMetadata(string jobName);
}
