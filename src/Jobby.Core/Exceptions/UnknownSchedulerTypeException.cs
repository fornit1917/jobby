namespace Jobby.Core.Exceptions;

public class UnknownSchedulerTypeException : Exception
{
    public UnknownSchedulerTypeException(string message) : base(message)
    {
    }
}