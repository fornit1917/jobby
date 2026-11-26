using System.Collections.Frozen;
using System.Text.Json;
using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Core.Services;

namespace Jobby.Dashboard;

// Creates jobs for the dashboard through the Jobby client of the host application, so a job created here is
// the same as one created from code: same serializer, default options of the command, exclusive recurrent
// definitions. Only jobs registered in the host (AddJob / AddJobsFromAssemblies) can be created, because
// the command type is needed to read the submitted parameters.
internal sealed class DashboardJobCreator
{
    // Parameters are typed by hand, where "recipient" is as natural as "Recipient". These options only read
    // the typed text: the command is then passed to the client, which stores it in the format of Jobby.
    private static readonly JsonSerializerOptions ParametersOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IJobbyClient? _client;
    private readonly FrozenDictionary<string, IJobCreator> _creatorsByJobName;

    // Both services are optional: an application can host the dashboard without registering Jobby itself.
    // The last registration is taken, as it is the one the application itself resolves.
    public DashboardJobCreator(IEnumerable<JobbyBuilder> builders, IEnumerable<IJobbyClient> clients)
    {
        var builder = builders.LastOrDefault();
        _client = clients.LastOrDefault();

        var commandTypes = builder is null || _client is null
            ? []
            : builder.AddedJobTypes.Select(types => types.CommandType);
        _creatorsByJobName = commandTypes
            .Select(commandType => (IJobCreator)Activator.CreateInstance(
                typeof(JobCreator<>).MakeGenericType(commandType))!)
            .ToFrozenDictionary(creator => creator.JobName);
        JobNames = _creatorsByJobName.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Empty when the host has no Jobby client or no registered jobs.
    public IReadOnlyList<string> JobNames { get; }

    public async Task<Guid> EnqueueAsync(CreateJobRequest request)
    {
        var creator = GetCreator(request.JobName);
        var command = DeserializeCommand(creator, request.JobParam);
        var opts = new JobOpts { QueueName = TrimToNull(request.QueueName) };
        return await creator.EnqueueAsync(_client!, command, opts);
    }

    public async Task<Guid> ScheduleRecurrentAsync(CreateRecurrentJobRequest request)
    {
        var creator = GetCreator(request.JobName);
        var command = DeserializeCommand(creator, request.JobParam);
        var opts = new RecurrentJobOpts
        {
            QueueName = TrimToNull(request.QueueName),
            IsExclusive = request.IsExclusive,
        };

        try
        {
            return await creator.ScheduleRecurrentAsync(_client!, command, request.Cron ?? string.Empty, opts);
        }
        catch (InvalidScheduleException e)
        {
            throw new DashboardJobCreationException(e.Message);
        }
    }

    private IJobCreator GetCreator(string? jobName)
    {
        if (jobName is not null && _creatorsByJobName.TryGetValue(jobName, out var creator))
        {
            return creator;
        }

        throw new DashboardJobCreationException($"Job '{jobName}' is not registered in this application.");
    }

    private static object DeserializeCommand(IJobCreator creator, string? jobParam)
    {
        object? command;
        try
        {
            command = jobParam is null
                ? null
                : JsonSerializer.Deserialize(jobParam, creator.CommandType, ParametersOptions);
        }
        catch (Exception e)
        {
            // Besides JSON errors, a constructor or a converter of the command can throw anything; whatever
            // is rejected here is text typed by the user.
            throw new DashboardJobCreationException($"Parameters do not match job '{creator.JobName}': {e.Message}");
        }

        return command
               ?? throw new DashboardJobCreationException($"Parameters of job '{creator.JobName}' are required.");
    }

    // A queue name with stray spaces would name a queue that no server reads.
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Binds the generic client methods to one command type, the way JobExecutor<,> does for job execution.
    private interface IJobCreator
    {
        string JobName { get; }
        Type CommandType { get; }
        Task<Guid> EnqueueAsync(IJobbyClient client, object command, JobOpts opts);
        Task<Guid> ScheduleRecurrentAsync(IJobbyClient client, object command, string cron, RecurrentJobOpts opts);
    }

    private sealed class JobCreator<TCommand> : IJobCreator where TCommand : IJobCommand
    {
        public string JobName => TCommand.GetJobName();

        public Type CommandType => typeof(TCommand);

        public Task<Guid> EnqueueAsync(IJobbyClient client, object command, JobOpts opts)
            => client.EnqueueCommandAsync((TCommand)command, opts);

        public Task<Guid> ScheduleRecurrentAsync(IJobbyClient client, object command, string cron,
            RecurrentJobOpts opts)
            => client.ScheduleRecurrentAsync((TCommand)command, cron, opts);
    }
}
