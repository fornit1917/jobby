using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jobby.IntegrationTests.Postgres.Helpers;

[Table("jobby_unlocking_groups")]
public class UnlockingGroupDbModel
{
    [Column("group_id")]
    [Key]
    public string GroupId { get; set; } = string.Empty;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}