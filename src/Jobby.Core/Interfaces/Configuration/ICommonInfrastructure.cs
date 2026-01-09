using Microsoft.Extensions.Logging;

namespace Jobby.Core.Interfaces.Configuration;

public interface ICommonInfrastructure
{
    ILoggerFactory LoggerFactory { get; }
    IJobParamSerializer Serializer { get; }
    IGuidGenerator GuidGenerator { get; }
}