using Microsoft.Extensions.Logging;

namespace Jobby.Core.Interfaces.Configuration;

public interface ICommonInfrastructure
{
    public ILoggerFactory LoggerFactory { get; }
    public IJobParamSerializer Serializer { get; }
}