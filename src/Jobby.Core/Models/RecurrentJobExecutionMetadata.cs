using System.Reflection;

namespace Jobby.Core.Models;

public class RecurrentJobExecutionMetadata
{
    public required Type HandlerType { get; init; }
    public required MethodInfo ExecMethod { get; init; }
}
