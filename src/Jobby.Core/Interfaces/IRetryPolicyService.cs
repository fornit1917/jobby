using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IRetryPolicyService
{
    TimeSpan? GetRetryInterval(JobModel job);
}
