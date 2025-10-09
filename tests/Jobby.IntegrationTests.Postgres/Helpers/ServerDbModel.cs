using System.ComponentModel.DataAnnotations.Schema;

namespace Jobby.IntegrationTests.Postgres.Helpers;

[Table("jobby_servers")]
public class ServerDbModel
{
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("heartbeat_ts")]
    public DateTime HeartbeatTs { get; set; }
}
