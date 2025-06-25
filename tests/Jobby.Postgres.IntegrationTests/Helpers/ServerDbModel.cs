using System.ComponentModel.DataAnnotations.Schema;

namespace Jobby.Postgres.IntegrationTests.Helpers;

[Table("jobby_servers")]
public class ServerDbModel
{
    [Column("id")]
    public string Id { get; set; }

    [Column("heartbeat_ts")]
    public DateTime HeartbeatTs { get; set; }
}
