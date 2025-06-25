using Microsoft.EntityFrameworkCore;

namespace Jobby.IntegrationTests.Postgres.Helpers;

internal class JobbyTestingDbContext : DbContext
{
    public JobbyTestingDbContext(DbContextOptions opts) : base(opts)
    {
    }

    public DbSet<JobDbModel> Jobs { get; set; }
    public DbSet<ServerDbModel> Servers { get; set; }
}
