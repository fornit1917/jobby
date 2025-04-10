namespace Jobby.Core.Dto;

public record struct JobIdWithNext(Guid JobId, Guid? NextJobId);
