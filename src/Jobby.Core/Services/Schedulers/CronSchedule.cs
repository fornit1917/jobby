using Jobby.Core.Interfaces.Schedulers;

namespace Jobby.Core.Services.Schedulers;

public class CronSchedule : ISchedule
{
    public string CronExpression { get; init; } = string.Empty;
    public bool CalculateNextFromPrev { get; init; }
}