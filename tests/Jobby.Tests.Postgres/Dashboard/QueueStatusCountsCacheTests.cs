using Jobby.Postgres.Dashboard;
using Jobby.TestsUtils;

namespace Jobby.Tests.Postgres.Dashboard;

public class QueueStatusCountsCacheTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(1);

    private readonly ManualTimeProvider _time = new();
    private readonly List<TaskCompletionSource<QueueStatusCounts>> _loads = [];
    private readonly List<CancellationToken> _loadTokens = [];

    private QueueStatusCountsCache CreateCache(TimeSpan duration) => new(StartLoad, duration, _time);

    private Task<QueueStatusCounts> StartLoad(CancellationToken cancellationToken)
    {
        var load = new TaskCompletionSource<QueueStatusCounts>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loads.Add(load);
        _loadTokens.Add(cancellationToken);
        return load.Task;
    }

    private static QueueStatusCounts NewCounts() => new([]);

    [Fact]
    public async Task GetAsync_WithinDuration_ReusesResultWithoutLoadingAgain()
    {
        var cache = CreateCache(Duration);
        var counts = NewCounts();

        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        _loads[0].SetResult(counts);
        Assert.Same(counts, await first);
        _time.Advance(Duration - TimeSpan.FromSeconds(1));

        Assert.Same(counts, await cache.GetAsync(TestContext.Current.CancellationToken));
        Assert.Single(_loads);
    }

    [Fact]
    public async Task GetAsync_AfterDurationElapsed_LoadsAgain()
    {
        var cache = CreateCache(Duration);
        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        _loads[0].SetResult(NewCounts());
        await first;
        _time.Advance(Duration);

        var fresh = NewCounts();
        var second = cache.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _loads.Count);
        _loads[1].SetResult(fresh);

        Assert.Same(fresh, await second);
    }

    [Fact]
    public async Task GetAsync_DurationStartsWhenLoadCompletes()
    {
        var cache = CreateCache(Duration);
        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        _time.Advance(Duration * 10);
        var counts = NewCounts();
        _loads[0].SetResult(counts);
        await first;
        _time.Advance(Duration - TimeSpan.FromSeconds(1));

        Assert.Same(counts, await cache.GetAsync(TestContext.Current.CancellationToken));
        Assert.Single(_loads);
    }

    [Fact]
    public async Task GetAsync_WhileLoadIsRunning_JoinsItEvenAfterDurationElapsed()
    {
        var cache = CreateCache(Duration);
        var counts = NewCounts();

        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        _time.Advance(Duration * 10);
        var second = cache.GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_loads);
        _loads[0].SetResult(counts);
        Assert.Same(counts, await first);
        Assert.Same(counts, await second);
    }

    [Fact]
    public async Task GetAsync_FailedLoad_ReachesEveryWaiterAndIsNotReused()
    {
        var cache = CreateCache(Duration);
        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        var second = cache.GetAsync(TestContext.Current.CancellationToken);

        _loads[0].SetException(new InvalidOperationException("counts query failed"));

        Assert.Equal("counts query failed", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        Assert.Equal("counts query failed", (await Assert.ThrowsAsync<InvalidOperationException>(() => second)).Message);

        var counts = NewCounts();
        var third = cache.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _loads.Count);
        _loads[1].SetResult(counts);
        Assert.Same(counts, await third);
    }

    [Fact]
    public async Task GetAsync_CancelledWaiter_DoesNotCancelTheSharedLoad()
    {
        var cache = CreateCache(Duration);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = cache.GetAsync(cancelled.Token);
        var second = cache.GetAsync(TestContext.Current.CancellationToken);

        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => first);
        Assert.False(_loadTokens[0].CanBeCanceled);
        var counts = NewCounts();
        _loads[0].SetResult(counts);
        Assert.Same(counts, await second);
    }

    [Fact]
    public async Task Invalidate_DropsResult_SoNextCallLoadsAgain()
    {
        var cache = CreateCache(Duration);
        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        _loads[0].SetResult(NewCounts());
        await first;

        cache.Invalidate();

        var fresh = NewCounts();
        var second = cache.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _loads.Count);
        _loads[1].SetResult(fresh);
        Assert.Same(fresh, await second);
    }

    [Fact]
    public async Task Invalidate_DuringLoad_NextCallStartsNewLoadInsteadOfJoiningTheOldOne()
    {
        var cache = CreateCache(Duration);
        var beforeChange = cache.GetAsync(TestContext.Current.CancellationToken);

        cache.Invalidate();
        var afterChange = cache.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _loads.Count);
        var stale = NewCounts();
        var fresh = NewCounts();
        _loads[0].SetResult(stale);
        _loads[1].SetResult(fresh);
        Assert.Same(stale, await beforeChange);
        Assert.Same(fresh, await afterChange);
        Assert.Same(fresh, await cache.GetAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, _loads.Count);
    }

    [Fact]
    public async Task GetAsync_ZeroDuration_SharesOnlyTheRunningLoad()
    {
        var cache = CreateCache(TimeSpan.Zero);
        var counts = NewCounts();

        var first = cache.GetAsync(TestContext.Current.CancellationToken);
        var second = cache.GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_loads);
        _loads[0].SetResult(counts);
        Assert.Same(counts, await first);
        Assert.Same(counts, await second);

        var fresh = NewCounts();
        var third = cache.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _loads.Count);
        _loads[1].SetResult(fresh);
        Assert.Same(fresh, await third);
    }

    [Fact]
    public async Task TryGetLoaded_ReturnsCountsOnlyWhileTheyAreLoadedAndReusable()
    {
        var cache = CreateCache(Duration);
        Assert.False(cache.TryGetLoaded(out _));

        var load = cache.GetAsync(TestContext.Current.CancellationToken);
        Assert.False(cache.TryGetLoaded(out _));

        var counts = NewCounts();
        _loads[0].SetResult(counts);
        await load;
        Assert.True(cache.TryGetLoaded(out var loaded));
        Assert.Same(counts, loaded);

        _time.Advance(Duration);
        Assert.False(cache.TryGetLoaded(out _));
        Assert.Single(_loads);
    }

    [Fact]
    public async Task TryGetLoaded_AfterInvalidateOrFailedLoad_ReturnsFalse()
    {
        var cache = CreateCache(Duration);
        var load = cache.GetAsync(TestContext.Current.CancellationToken);
        _loads[0].SetResult(NewCounts());
        await load;

        cache.Invalidate();
        Assert.False(cache.TryGetLoaded(out _));

        var failing = cache.GetAsync(TestContext.Current.CancellationToken);
        _loads[1].SetException(new InvalidOperationException("counts query failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
        Assert.False(cache.TryGetLoaded(out _));
    }
}
