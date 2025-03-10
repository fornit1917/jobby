﻿using System.Reflection;

namespace Jobby.Core.Models;

public class CommandExecutionMetadata
{
    public required Type CommandType { get; init; }
    public required Type HandlerType { get; init; }
    public required MethodInfo ExecMethod { get; init; }
}
