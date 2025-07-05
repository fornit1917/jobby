using Jobby.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Jobby.Samples.AspNet.Db;

public class JobbySampleDbContext : DbContext
{
    public DbSet<JobCreationModel> Jobs { get; set; }

    public JobbySampleDbContext(DbContextOptions<JobbySampleDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobCreationModel>().ToTable("jobby_jobs");
        modelBuilder.Entity<JobCreationModel>().HasKey(x => x.Id);
    }
}
