using Jobby.Core.Interfaces;
using System.Reflection;

namespace Jobby.Core.Models;

internal class JobExecutionMetadata : IJobTypesMetadata
{
    public required Type CommandType { get; init; }
    public required Type HandlerType { get; init; }
    public required Type HandlerImplType { get; init; }
    public required MethodInfo ExecMethod { get; init; }
}
