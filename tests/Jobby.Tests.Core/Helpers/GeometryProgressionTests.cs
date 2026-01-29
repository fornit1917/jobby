using Jobby.Core.Helpers;

namespace Jobby.Tests.Core.Helpers;

public class GeometryProgressionTests
{
    [Fact]
    public void GetCurrentValueAndSetToNext_StartMoreThanMax_ReturnsMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 50);

        Assert.Equal(50, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(50, geometryProgression.GetCurrentValueAndSetToNext());
    }

    [Fact]
    public void GetCurrentValueAndSetToNext_StartLessThanMax_ReturnsIncreasingValuesUpToMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 1000);

        Assert.Equal(100, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(200, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(400, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(800, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(1000, geometryProgression.GetCurrentValueAndSetToNext());
        Assert.Equal(1000, geometryProgression.GetCurrentValueAndSetToNext());
    }

    [Fact]
    public void Reset_StartLessThanMax_ResetsToStart()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 1000);

        geometryProgression.GetCurrentValueAndSetToNext();
        geometryProgression.GetCurrentValueAndSetToNext();
        geometryProgression.Reset();

        Assert.Equal(100, geometryProgression.GetCurrentValueAndSetToNext());
    }

    [Fact]
    public void Reset_StartMoreThanMax_ResetsToMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 50);

        geometryProgression.GetCurrentValueAndSetToNext();
        geometryProgression.GetCurrentValueAndSetToNext();
        geometryProgression.Reset();

        Assert.Equal(50, geometryProgression.GetCurrentValueAndSetToNext());
    }

    [Fact]
    public void CurrentValue_AlwaysReturnsCurrent()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 1000);
        
        Assert.Equal(100, geometryProgression.CurrentValue);
        Assert.Equal(100, geometryProgression.CurrentValue);

        geometryProgression.GetCurrentValueAndSetToNext();
        
        Assert.Equal(200, geometryProgression.CurrentValue);
        Assert.Equal(200, geometryProgression.CurrentValue);
    }
}
