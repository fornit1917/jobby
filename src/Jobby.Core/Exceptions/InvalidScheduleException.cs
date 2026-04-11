namespace Jobby.Core.Exceptions;

public class InvalidScheduleException : Exception
{
    public InvalidScheduleException(string message) : base(message) { }
    public InvalidScheduleException(string message, Exception inner) : base(message, inner) { }
}
