using System.Diagnostics.CodeAnalysis;

namespace Jobby.Core.Interfaces;

public interface IJobParamSerializer
{
    string SerializeJobParam<T>(T param);
    object? DeserializeJobParam(string? json, Type jobParamType);
}

public interface IJobParamSerializer<T>
{
    string SerializeJobParam(T param);
    bool TryDeserializeJobParam(string value, [NotNullWhen(true)] out T? param);
}
