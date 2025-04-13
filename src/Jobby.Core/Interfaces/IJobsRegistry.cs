using Jobby.Core.Models;
using System.Reflection;

namespace Jobby.Core.Interfaces;

internal interface IJobsRegistry
{
    CommandExecutionMetadata? GetCommandExecutionMetadata(string jobName);
    RecurrentJobExecutionMetadata? GetRecurrentJobExecutionMetadata(string jobName);
}
