namespace Jobby.Core.Models;

public class StuckJobModel
{
    public Guid Id { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string ServerId { get; init; } = string.Empty;
    public bool CanBeRestarted { get; init; }
}
