namespace Jobby.Core.Exceptions;

public class InvalidJobHandlerException : Exception
{
    public InvalidJobHandlerException(string message) : base(message)
    {
    }
}
