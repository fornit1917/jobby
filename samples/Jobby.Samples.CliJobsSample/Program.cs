using Jobby.Abstractions.Models;
using Jobby.Core.Client;
using Jobby.Postgres.Client;
using Npgsql;
using System.Text.Json;

namespace Jobby.Samples.CliJobsSample;

internal class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db";
        using var dataSource = NpgsqlDataSource.Create(connectionString);
        var pgJobsStorage = new PgJobsStorage(dataSource);
        var jobsClient = new JobsClient(pgJobsStorage);

        for (int i = 1; i <= 100; i++)
        {
            var jobParam = new TestJobParam { Id = i, Name = "SomeValue" };
            var job = new JobModel
            {
                JobName = "TestJob",
                JobParam = JsonSerializer.Serialize(jobParam),
            };
            await jobsClient.EnqueueAsync(job);
            Console.WriteLine(job.Id);
        }


        Console.ReadLine();
    }

    private class TestJobParam
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
