using System.Diagnostics.CodeAnalysis;

using Jobby.Core.Interfaces;

namespace Jobby.Core.Models;

public class DefaultJobParamSerializer<T> : IJobParamSerializer<T>
{
    private readonly IJobParamSerializer _defaultSerializer;

    public DefaultJobParamSerializer(IJobParamSerializer defaultSerializer)
    {
        _defaultSerializer = defaultSerializer ?? throw new ArgumentNullException(nameof(defaultSerializer));
    }

    public string SerializeJobParam(T param) => _defaultSerializer.SerializeJobParam(param);

    public bool TryDeserializeJobParam(string value, [NotNullWhen(true)] out T? param)
    {
        param = (T?)_defaultSerializer.DeserializeJobParam(value, typeof(T));
        return param is not null;
    }
}
