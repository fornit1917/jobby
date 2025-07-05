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
}
