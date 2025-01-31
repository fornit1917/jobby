using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using System.Text.Json;

namespace Jobby.Core.CommonServices;

internal class SystemTextJsonJobParamSerializer : IJobParamSerializer
{
    private readonly JsonSerializerOptions _opts;

    public SystemTextJsonJobParamSerializer(JsonSerializerOptions opts)
    {
        _opts = opts;
    }

    public T? DeserializeJobParam<T>(string serializedJobParam) where T : IJobCommand
    {
        return JsonSerializer.Deserialize<T>(serializedJobParam, _opts);
    }

    public string SerializeJobParam<T>(T command) where T : IJobCommand
    {
        return JsonSerializer.Serialize(command, _opts);
    }
}
