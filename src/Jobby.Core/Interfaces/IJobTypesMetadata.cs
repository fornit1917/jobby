namespace Jobby.Core.Interfaces;

public interface IJobTypesMetadata
{
    public Type CommandType { get; }
    public Type HandlerType { get; }
    public Type HandlerImplType { get; }
}
