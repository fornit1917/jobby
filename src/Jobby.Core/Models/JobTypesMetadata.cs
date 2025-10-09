namespace Jobby.Core.Models;

public readonly record struct JobTypesMetadata(Type CommandType, Type HandlerType, Type HandlerImplType);
