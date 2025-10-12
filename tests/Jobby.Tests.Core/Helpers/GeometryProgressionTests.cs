using Jobby.Core.Helpers;

namespace Jobby.Tests.Core.Helpers;

public class GeometryProgressionTests
{
    [Fact]
    public void GetNextValue_StartMoreThanMax_ReturnsMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 50);

        Assert.Equal(50, geometryProgression.GetNextValue());
        Assert.Equal(50, geometryProgression.GetNextValue());
    }

    [Fact]
    public void GetNextValue_StartLessThanMax_ReturnsIncreasingValuesUpToMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 1000);

        Assert.Equal(100, geometryProgression.GetNextValue());
        Assert.Equal(200, geometryProgression.GetNextValue());
        Assert.Equal(400, geometryProgression.GetNextValue());
        Assert.Equal(800, geometryProgression.GetNextValue());
        Assert.Equal(1000, geometryProgression.GetNextValue());
        Assert.Equal(1000, geometryProgression.GetNextValue());
    }

    [Fact]
    public void Reset_StartLessThanMax_ResetsToStart()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 1000);

        geometryProgression.GetNextValue();
        geometryProgression.GetNextValue();
        geometryProgression.Reset();

        Assert.Equal(100, geometryProgression.GetNextValue());
    }

    [Fact]
    public void Reset_StartMoreThanMax_ResetsToMax()
    {
        var geometryProgression = new GeometryProgression(start: 100, factor: 2, max: 50);

        geometryProgression.GetNextValue();
        geometryProgression.GetNextValue();
        geometryProgression.Reset();

        Assert.Equal(50, geometryProgression.GetNextValue());
    }
}
