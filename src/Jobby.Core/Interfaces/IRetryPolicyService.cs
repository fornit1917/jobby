﻿using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IRetryPolicyService
{
    RetryPolicy GetRetryPolicy(JobExecutionModel job);
}
