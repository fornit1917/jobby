namespace Jobby.Core.Exceptions;

public class CronException : Exception
{
    public CronException(string message) : base(message) { }
    public CronException(string message, Exception inner) : base(message, inner) { }
}
