namespace Jobby.Samples.Benchmarks;

public static class Counter
{
    public static int Value = 0;

    public static int CompletedValue = 1000;

    public static ManualResetEvent Event = new ManualResetEvent(false);

    public static void Increment()
    {
        var newValue = Interlocked.Increment(ref Value);
        if (newValue == CompletedValue)
        {
            Event.Set();
        }
    }

    public static void Reset(int completedValue)
    {
        Value = 0;
        CompletedValue = completedValue;
        Event.Reset();
    }
}
