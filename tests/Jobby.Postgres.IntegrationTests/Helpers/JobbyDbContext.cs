using Microsoft.EntityFrameworkCore;

namespace Jobby.Postgres.IntegrationTests.Helpers;

internal class JobbyDbContext : DbContext
{
    public JobbyDbContext(DbContextOptions opts) : base(opts)
    {
    }

    public DbSet<JobDbModel> Jobs { get; set; }
}
