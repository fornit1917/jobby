using Jobby.Core.Interfaces;
using System.Text.Json;

namespace Jobby.Core.Services;

public class SystemTextJsonJobParamSerializer : IJobParamSerializer
{
    private readonly JsonSerializerOptions _opts;

    public SystemTextJsonJobParamSerializer(JsonSerializerOptions opts)
    {
        _opts = opts;
    }

    public object? DeserializeJobParam(string? json, Type jobParamType)
    {
        return json == null
            ? null
            : JsonSerializer.Deserialize(json, jobParamType, _opts);
    }

    public string SerializeJobParam<T>(T command) where T : IJobCommand
    {
        return JsonSerializer.Serialize(command, _opts);
    }
}
