using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.CommonServices;

public interface IJobParamSerializer
{
    string SerializeJobParam<T>(T command) where T : IJobCommand;
    T? DeserializeJobParam<T>(string serializedJobParam) where T : IJobCommand;
}
