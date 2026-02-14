using System.Collections.Concurrent;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.TestConcurrency;

public class ConcurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "ConcurrencyDemo";
    public bool CanBeRestarted() => true;
    
    public string Id { get; init; }
    public int TotalCount { get; init; }
    public int DelayMs { get; init; }
    public string? SerializableGroupId { get; init; }
}

public class ConcurrentJobCommandHandler : IJobCommandHandler<ConcurrentJobCommand>
{
    public static int ExecutedCount = 0;
    
    public static int InTimeWithoutGroup = 0;
    public static bool WithoutGroupExecutedInParallel = false;
    
    public static ConcurrentDictionary<string, int> InTimeByGroupId = new();
    public static ConcurrentDictionary<string, bool> GroupsExecutedInParallel = new();
    
    public static readonly TaskCompletionSource JobsCompletedTcs = new TaskCompletionSource();
    
    public async Task ExecuteAsync(ConcurrentJobCommand command, JobExecutionContext ctx)
    {
        if (command.SerializableGroupId == null)
        {
            var inTimeWithoutGroup = Interlocked.Increment(ref InTimeWithoutGroup);
            if (inTimeWithoutGroup > 1)
            {
                WithoutGroupExecutedInParallel = true;
            }
        }
        else
        {
            var inTimeWithGroup = InTimeByGroupId
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
            Interlocked.Decrement(ref InTimeWithoutGroup);
        }
        else
        {
            InTimeByGroupId.AddOrUpdate(command.SerializableGroupId, 0, (key, oldValue) => oldValue - 1);
        }
    }
}