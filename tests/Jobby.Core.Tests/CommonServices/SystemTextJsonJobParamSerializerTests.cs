using Jobby.Abstractions.Models;
using Jobby.Core.CommonServices;
using System.Text.Json;

namespace Jobby.Core.Tests.CommonServices;

public class SystemTextJsonJobParamSerializerTests
{
    [Fact]
    public void SerializesAndDeserializes()
    {
        var serializer = new SystemTextJsonJobParamSerializer(new JsonSerializerOptions());
        var param = new TestParam
        {
            Id = 123,
            Name = "test",
        };

        var serializedParam = serializer.SerializeJobParam(param);
        var deserializedParam = serializer.DeserializeJobParam(serializedParam, typeof(TestParam)) as TestParam;

        Assert.NotNull(deserializedParam);
        Assert.Equal(param.Id, deserializedParam.Id);
        Assert.Equal(param.Name, deserializedParam.Name);
    }

    private class TestParam : IJobCommand
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        public static string GetJobName() => "TestJobName";
    }
}
