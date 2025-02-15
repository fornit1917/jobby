using System.Text.Json;
using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;
using Jobby.Core.Server;

namespace Jobby.Samples.Benchmarks.JobbyBenchmarks;

public class JobbyTestExecutionScope : JobExecutionScopeBase
{
    public JobbyTestExecutionScope(IReadOnlyDictionary<string, Type> jobCommandTypesByName, IReadOnlyDictionary<Type, Type> handlerTypesByCommandType, IJobParamSerializer serializer) 
        : base(jobCommandTypesByName, handlerTypesByCommandType, serializer)
    {
    }

    public override void Dispose()
    {
    }

    protected override object? CreateService(Type t)
    {
        if (t == typeof(IJobCommandHandler<JobbyTestJobCommand>))
        {
            return new JobbyTestJobCommandHandler();
        }
        return null;
    }
}

public class JobbyTestExecutionScopeFactory : IJobExecutionScopeFactory
{
    private readonly IJobParamSerializer _serializer;
    private readonly Dictionary<string, Type> _jobCommandTypesByName;
    private readonly Dictionary<Type, Type> _jobHandlerTypesByCommandTypes;

    public JobbyTestExecutionScopeFactory(IJobParamSerializer serializer) 
    {
        _serializer = serializer;
        _jobCommandTypesByName = new Dictionary<string, Type>()
        {
            ["TestJob"] = typeof(JobbyTestJobCommand)
        };
        _jobHandlerTypesByCommandTypes = new Dictionary<Type, Type>()
        {
            [typeof(JobbyTestJobCommand)] = typeof(IJobCommandHandler<JobbyTestJobCommand>)
        };
    }

    public IJobExecutionScope CreateJobExecutionScope()
    {
        return new JobbyTestExecutionScope(_jobCommandTypesByName, _jobHandlerTypesByCommandTypes, _serializer);
    }
}