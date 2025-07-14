using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hangfire;
using System.Transactions;

namespace Jobby.Benchmarks.HangfireBenchmarks;

public class HangfireBulkCreateJobsBenchmark : IBenchmark
{
    public string Name => "Hangfire.BulkCreate.TransactionScope.10";

    public Task Run()
    {
        BenchmarkRunner.Run<HangfireBulkCreateJobsBenchmarkAction>();
        return Task.CompletedTask;
    }
}

[MemoryDiagnoser]
[WarmupCount(10)]
[IterationCount(10)]
public class HangfireBulkCreateJobsBenchmarkAction
{
    public HangfireBulkCreateJobsBenchmarkAction()
    {
        var dataSource = DataSourceFactory.Create(enlist: true);
        HangfireHelper.ConfigureGlobal(dataSource);
    }

    [Benchmark]
    public void HangfireBulkCreateJobs()
    {
        const int jobsCount = 10;
        
        var trOpts = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted
        };
        using var transactionScope = new TransactionScope(
            TransactionScopeOption.Required,
            trOpts,
            TransactionScopeAsyncFlowOption.Enabled);

        for (int i = 1; i <= jobsCount; i++)
        {
            var jobParam = new HangfireTestJobParam
            {
                Id = i,
                Value = Guid.NewGuid().ToString(),
            };
            BackgroundJob.Enqueue<HangfireTestJob>(x => x.Execute(jobParam));
        }

        transactionScope.Complete();
    }
}
