namespace Jobby.Samples.AspNet.Exceptions;

public class ExceptionShouldBeIgnored : Exception
{
    public ExceptionShouldBeIgnored(string message): base(message)
    {
    }
}
