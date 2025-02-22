using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IRecurrentJobHandler
{
    static abstract string GetRecurrentJobName();

    Task ExecuteAsync(RecurrentJobExecutionContext ctx);
}
