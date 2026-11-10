using Jobby.TestsUtils.Jobs;

namespace Jobby.TestsUtils;

public class ExecutedCommandsList
{
    private readonly List<TestJobCommand> _commands = new List<TestJobCommand>();

    public void Add(TestJobCommand command)
    {
        lock (_commands)
        {
            _commands.Add(command);
        }
    }

    public void Clear()
    {
        lock (_commands)
        {
            _commands.Clear();
        }
    }

    public bool HasCommandWithId(Guid uniqueId)
    {
        lock (_commands)
        {
            return _commands.Any(x => x.UniqueId == uniqueId);
        }
    }

    /// <summary>
    /// Returns unique ids of executed commands in execution order.
    /// </summary>
    public IReadOnlyList<Guid> GetUniqueIdsInExecutionOrder()
    {
        lock (_commands)
        {
            return _commands.Select(x => x.UniqueId).ToList();
        }
    }

    /// <summary>
    /// Returns how many times a command with the given unique id was executed.
    /// </summary>
    public int CountExecutions(Guid uniqueId)
    {
        lock (_commands)
        {
            return _commands.Count(x => x.UniqueId == uniqueId);
        }
    }
}
