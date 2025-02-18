namespace Jobby.Core.Interfaces;

public interface IJobParamSerializer
{
    string SerializeJobParam<T>(T command) where T : IJobCommand;
    object? DeserializeJobParam(string? json, Type jobParamType);
}
