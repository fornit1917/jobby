namespace Jobby.Core.Interfaces.Builders;

public interface IRetryPolicyBuilder
{
    IRetryPolicyService Build();
}
