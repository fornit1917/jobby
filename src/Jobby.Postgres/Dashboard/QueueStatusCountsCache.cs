using Jobby.Postgres.Dashboard.Commands;
using Npgsql;

namespace Jobby.Postgres.Dashboard;

// Shares one queue/status aggregate between dashboard requests, so its cost does not grow with the number of
// open dashboards. Requests arriving while the query runs wait for that query instead of starting another one,
// and its result is reused for the configured duration. The cache belongs to one application instance: every
// instance keeps its own result, each at most that duration old.
internal sealed class QueueStatusCountsCache
{
    private readonly Func<CancellationToken, Task<QueueStatusCounts>> _load;
    private readonly TimeSpan _duration;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private Entry? _entry;

    public QueueStatusCountsCache(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings, TimeSpan duration,
        TimeProvider? timeProvider = null)
        : this(new GetQueueStatusCountsCommand(dataSource, settings).ExecuteAsync, duration, timeProvider)
    {
    }

    public QueueStatusCountsCache(Func<CancellationToken, Task<QueueStatusCounts>> load, TimeSpan duration,
        TimeProvider? timeProvider = null)
    {
        _load = load;
        _duration = duration;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Returns the shared counts, reading them first unless a reusable result or a running read exists.
    public Task<QueueStatusCounts> GetAsync(CancellationToken cancellationToken)
    {
        Entry entry;
        Entry? started = null;
        lock (_gate)
        {
            if (_entry is null || !IsUsable(_entry))
            {
                _entry = started = new Entry();
            }

            entry = _entry;
        }

        if (started is not null)
        {
            _ = LoadAsync(started);
        }

        return entry.Completion.Task.WaitAsync(cancellationToken);
    }

    // Returns counts that are already loaded and still reusable. It never starts or waits for the aggregate
    // query, so a caller with a cheaper way to count can fall back to it.
    public bool TryGetLoaded(out QueueStatusCounts counts)
    {
        Entry? entry;
        lock (_gate)
        {
            entry = _entry;
        }

        if (entry is not null && entry.Completion.Task.IsCompletedSuccessfully && IsUsable(entry))
        {
            counts = entry.Completion.Task.Result;
            return true;
        }

        counts = null!;
        return false;
    }

    // Drops the current result so the next request reads the counts again.
    public void Invalidate()
    {
        lock (_gate)
        {
            _entry = null;
        }
    }

    private bool IsUsable(Entry entry)
    {
        var task = entry.Completion.Task;
        if (!task.IsCompleted)
        {
            return true;
        }

        return task.IsCompletedSuccessfully
               && _timeProvider.GetElapsedTime(Volatile.Read(ref entry.CompletedAt)) < _duration;
    }

    // The shared query is not bound to the request that started it: other requests may be waiting for it too.
    private async Task LoadAsync(Entry entry)
    {
        try
        {
            var counts = await _load(CancellationToken.None);
            Volatile.Write(ref entry.CompletedAt, _timeProvider.GetTimestamp());
            entry.Completion.SetResult(counts);
        }
        catch (Exception exception)
        {
            entry.Completion.SetException(exception);
            // Every waiting request may have been cancelled; mark the failure as observed.
            _ = entry.Completion.Task.Exception;
        }
    }

    private sealed class Entry
    {
        public readonly TaskCompletionSource<QueueStatusCounts> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long CompletedAt;
    }
}
