using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers.Serializers;
public class SystemTextJsonSchedulerSerializer<TScheduler> : IScheduleSerializer<TScheduler>
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SystemTextJsonSchedulerSerializer(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _jsonSerializerOptions = jsonSerializerOptions ?? JsonSerializerOptions.Default;
    }

    public string Serealize(TScheduler scheduler) => JsonSerializer.Serialize<TScheduler>(scheduler, _jsonSerializerOptions);

    public bool TryDeserialize(string value, [NotNullWhen(true)] out TScheduler? scheduler)
    {
        var result = JsonSerializer.Deserialize<TScheduler>(value, _jsonSerializerOptions);

        if (result is null)
        {
            scheduler = default;
            return false;
        }
        else
        {
            scheduler = result;
            return true;
        }
    }
}
