namespace Jobby.Core.Models;

public readonly record struct ProcessingJobsList(IReadOnlyList<Guid> JobIds, string ServerId);
