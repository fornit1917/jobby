using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils;
using Jobby.TestsUtils.Jobs;

namespace Jobby.IntegrationTests.Postgres.Helpers;

public class FactoryHelper
{
    private static readonly JobbyBuilder _builder = new JobbyBuilder();
    private static readonly IJobbyStorage _storage = DbHelper.CreateJobbyStorage();
    public static ExecutedCommandsList ExecutedCommands { get; } = new ExecutedCommandsList();

    public static IJobbyClient CreateJobbyClient()
    {
        _builder.UseStorage(_storage);
        return _builder.CreateJobbyClient();
    }

    public static IJobsFactory CreateJobsFactory()
    {
        return _builder.CreateJobsFactory();
    }

    public static IJobbyServer CreateJobbyServer(JobbyServerSettings serverSettings)
    {
        _builder.UseStorage(_storage);
        _builder.UseExecutionScopeFactory(new TestJobbyExecutionScopeFactory(ExecutedCommands));
        _builder.UseServerSettings(serverSettings);
        _builder.AddOrReplaceJob<TestJobCommand, TestJobCommandHandler>();
        return _builder.CreateJobbyServer();
    }
}
