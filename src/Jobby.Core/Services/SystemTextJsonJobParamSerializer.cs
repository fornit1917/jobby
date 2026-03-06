using System.Text.Json;

using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class SystemTextJsonJobParamSerializer : IJobParamSerializer
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

    public string SerializeJobParam<T>(T param)
    {
        return JsonSerializer.Serialize(param, _opts);
    }
}
