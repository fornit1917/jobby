using System.Reflection;

namespace Jobby.Core.Models;

internal class RecurrentJobExecutionMetadata
{
    public required Type HandlerType { get; init; }
    public required MethodInfo ExecMethod { get; init; }
}
