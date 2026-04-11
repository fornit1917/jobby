namespace Jobby.Core.Models;

public class GroupUnlockingStatusModel
{
    public string GroupId { get; init; } = string.Empty;
    public bool IsUnlocked { get; init; }
}