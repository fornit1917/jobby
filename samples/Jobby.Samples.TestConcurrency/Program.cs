using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.Postgres.ConfigurationExtensions;
using Npgsql;

namespace Jobby.Samples.TestConcurrency;

public static class Program
{
    private const int TotalJobs = 10000;
    
    public static async Task Main(string[] args)
    {
        var connectionString = "Host=localhost;Username=test_user;Password=12345;Database=test_db;GSS Encryption Mode=Disable";
        var dataSource = NpgsqlDataSource.Create(connectionString);

        Console.WriteLine("Start creating jobs");
        const int jobsPerClient = 5000;
        var client = CreateJobbyClient(dataSource);
        _ = RunJobCreationThread(client, clientId: 1, jobsPerClient);
        _ = RunJobCreationThread(client, clientId: 2, jobsPerClient);
        
        var firstServer = CreateJobbyServer(dataSource);
        var secondServer = CreateJobbyServer(dataSource);
        
        Console.WriteLine("Run servers");
        firstServer.StartBackgroundService();
        secondServer.StartBackgroundService();

        await ConcurrentJobCommandHandler.JobsCompletedTcs.Task;
        
        Console.WriteLine("Jobs executed: " + ConcurrentJobCommandHandler.ExecutedCount);
        Console.WriteLine("Without group executed in parallel: " + ConcurrentJobCommandHandler.WithoutGroupExecutedInParallel);
        Console.WriteLine("With group executed in parallel: " + ConcurrentJobCommandHandler.GroupsExecutedInParallel.Count);
        
        Console.WriteLine("Pause before stopping servers");
        await Task.Delay(2000);
        firstServer.SendStopSignal();
        secondServer.SendStopSignal();
        
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
        await dataSource.DisposeAsync();
    }

    private static IJobbyClient CreateJobbyClient(NpgsqlDataSource dataSource)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.AddJob<ConcurrentJobCommand, ConcurrentJobCommandHandler>();
        jobbyBuilder.UsePostgresql(dataSource);
        return jobbyBuilder.CreateJobbyClient();
    }

    private static IJobbyServer CreateJobbyServer(NpgsqlDataSource dataSource)
    {
        var jobbyBuilder = new JobbyBuilder();
        jobbyBuilder.AddJob<ConcurrentJobCommand, ConcurrentJobCommandHandler>();
        jobbyBuilder.UseExecutionScopeFactory(new ConcurrentJobExecutionScopeFactory());
        jobbyBuilder.UseServerSettings(new JobbyServerSettings
        {
            CompleteWithBatching = true,
            PollingIntervalMs = 1000,
            PollingIntervalStartMs = 50,
            PollingIntervalFactor = 2,
            MaxDegreeOfParallelism = 10,
            TakeToProcessingBatchSize = 10,
        });
        jobbyBuilder.UsePostgresql(dataSource);
        return jobbyBuilder.CreateJobbyServer();
    }

    private static Task RunJobCreationThread(IJobbyClient jobbyClient, int clientId, int jobsCount)
    {
        return Task.Run(async () =>
        {
            const int batchSize = 50;
            var jobs = new List<JobCreationModel>(capacity: batchSize);
            for (int i = 0; i < jobsCount; i++)
            {
                string? groupId = null;
                if (i % 10 < 5)
                {
                    groupId = $"group_{i % 10 + 1}";
                }
                var command = new ConcurrentJobCommand
                {
                    Id = $"{clientId}_{i + 1}",
                    TotalCount = TotalJobs,
                    SerializableGroupId = groupId
                };
                var opts = new JobOpts
                {
                    SerializableGroupId = groupId,
                };
                var job = jobbyClient.Factory.Create(command, opts);
                jobs.Add(job);

                if (jobs.Count == batchSize)
                {
                    await jobbyClient.EnqueueBatchAsync(jobs);
                    jobs.Clear();
                }
            }

            if (jobs.Count > 0)
            {
                await jobbyClient.EnqueueBatchAsync(jobs);
            }
            
            Console.WriteLine($"Client {clientId} created {jobsCount} jobs");
        });
    }
}