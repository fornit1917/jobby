namespace Jobby.Core.Helpers;

internal class GeometryProgression
{
    private readonly int _start;
    private readonly int _factor;
    private readonly int _max;

    public int CurrentValue { get; private set; }

    public GeometryProgression(int start, int factor, int max)
    {
        _start = start;
        _factor = factor;
        _max = max;
        Reset();
    }

    public void Reset()
    {
        CurrentValue = _start > _max ? _max : _start;
    }

    public int GetCurrentValueAndSetToNext()
    {
        var result = CurrentValue;
        
        if (CurrentValue < _max)
        {
            CurrentValue = CurrentValue * _factor;
            if (CurrentValue > _max)
            {
                CurrentValue = _max;
            }
        }

        return result;
    }
}
