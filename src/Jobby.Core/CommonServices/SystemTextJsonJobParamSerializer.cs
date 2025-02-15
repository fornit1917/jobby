using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using System.Text.Json;

namespace Jobby.Core.CommonServices;

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
