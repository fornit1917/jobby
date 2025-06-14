namespace Jobby.Core.Exceptions;

public class InvalidJobsConfigException : Exception
{
    public InvalidJobsConfigException(string message) : base(message)
    {
    }
}
