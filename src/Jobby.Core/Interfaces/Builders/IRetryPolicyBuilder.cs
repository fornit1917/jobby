namespace Jobby.Core.Interfaces.Builders;

internal interface IRetryPolicyBuilder
{
    IRetryPolicyService Build();
}
