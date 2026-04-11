namespace Jobby.Core.Models;

public readonly record struct JobWithGroupModel(Guid Id, string JobName, string GroupId);