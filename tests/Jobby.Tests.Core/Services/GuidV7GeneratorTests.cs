using Jobby.Core.Services;

namespace Jobby.Tests.Core.Services;

public class GuidV7GeneratorTests
{
    [Fact]
    public void GeneratesSequential()
    {
        var guidGenerator = new GuidV7Generator();
        var first = guidGenerator.NewGuid();
        var second = guidGenerator.NewGuid();
        Assert.True(first < second);
    }
}