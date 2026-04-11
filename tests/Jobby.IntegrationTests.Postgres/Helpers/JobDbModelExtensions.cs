using Jobby.Core.Models;

namespace Jobby.IntegrationTests.Postgres.Helpers;

internal static class JobDbModelListExtensions
{
    public static CompleteJobsBatch ToCompleteJobsBatch(this IList<JobDbModel> jobs, string serverId)
    {
        var completeJobsBatch = new CompleteJobsBatch
        {
            JobIds = jobs.Select(x => x.Id).ToList(),
            NextJobIds = jobs
                .Where(x => x.NextJobId.HasValue)
                .Select(x => x.NextJobId!.Value).ToList(),
            ServerId = serverId,
        };
        return completeJobsBatch;
    }
}