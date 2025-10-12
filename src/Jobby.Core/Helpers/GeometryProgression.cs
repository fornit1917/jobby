namespace Jobby.Core.Helpers;

internal class GeometryProgression
{
    private readonly int _start;
    private readonly int _factor;
    private readonly int _max;

    private int _nextValue;

    public GeometryProgression(int start, int factor, int max)
    {
        _start = start;
        _factor = factor;
        _max = max;
        Reset();
    }

    public void Reset()
    {
        _nextValue = _start > _max ? _max : _start;
    }

    public int GetNextValue()
    {
        var result = _nextValue;
        
        if (_nextValue < _max)
        {
            _nextValue = _nextValue * _factor;
            if (_nextValue > _max)
            {
                _nextValue = _max;
            }
        }

        return result;
    }
}
