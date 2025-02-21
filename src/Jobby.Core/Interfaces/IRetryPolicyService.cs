﻿using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IRetryPolicyService
{
    RetryPolicy GetRetryPolicy(JobModel job);
}
