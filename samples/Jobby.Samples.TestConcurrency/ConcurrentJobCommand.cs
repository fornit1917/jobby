using System.Collections.Concurrent;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.TestConcurrency;

public class ConcurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "ConcurrencyDemo";

    public string Id { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public int DelayMs { get; init; }
    public string? SerializableGroupId { get; init; }
}

public class ConcurrentJobCommandHandler : IJobCommandHandler<ConcurrentJobCommand>
{
    public static int ExecutedCount = 0;

    private static int _inParallelWithoutGroup = 0;
    public static bool WithoutGroupExecutedInParallel = false;

    private static readonly ConcurrentDictionary<string, int> InParallelByGroupId = new();
    public static readonly ConcurrentDictionary<string, bool> GroupsExecutedInParallel = new();
    
    public static readonly TaskCompletionSource JobsCompletedTcs = new TaskCompletionSource();
    
    public async Task ExecuteAsync(ConcurrentJobCommand command, JobExecutionContext ctx)
    {
        if (command.SerializableGroupId == null)
        {
            var inTimeWithoutGroup = Interlocked.Increment(ref _inParallelWithoutGroup);
            if (inTimeWithoutGroup > 1)
            {
                WithoutGroupExecutedInParallel = true;
            }
        }
        else
        {
            var inTimeWithGroup = InParallelByGroupId
                .AddOrUpdate(command.SerializableGroupId, 1, (key, oldValue) => oldValue + 1);
            if (inTimeWithGroup > 1)
            {
                GroupsExecutedInParallel.TryAdd(command.SerializableGroupId, true);
            }
        }
        
        if (command.DelayMs > 0)
        {
            await Task.Delay(command.DelayMs); 
        }
        
        var executedCount = Interlocked.Increment(ref ExecutedCount);
        if (executedCount == command.TotalCount)
        {
            JobsCompletedTcs.SetResult();
        }

        if (command.SerializableGroupId == null)
        {
            Interlocked.Decrement(ref _inParallelWithoutGroup);
        }
        else
        {
            InParallelByGroupId.AddOrUpdate(command.SerializableGroupId, 0, (key, oldValue) => oldValue - 1);
        }
    }
}