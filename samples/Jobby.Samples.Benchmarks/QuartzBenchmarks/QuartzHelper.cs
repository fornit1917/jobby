using Npgsql;
using Quartz;

namespace Jobby.Samples.Benchmarks.QuartzBenchmarks;

public static class QuartzHelper
{
    public static void RemoveAllJobs(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.OpenConnection();
        var cmd = new NpgsqlCommand("DELETE FROM qrtz_triggers;", conn);
        cmd.ExecuteNonQuery();
        cmd.Dispose();

        cmd = new NpgsqlCommand("DELETE FROM qrtz_simple_triggers;", conn);
        cmd.ExecuteNonQuery();
        cmd.Dispose();

        cmd = new NpgsqlCommand("DELETE FROM qrtz_job_details;", conn);
        cmd.ExecuteNonQuery();
        cmd.Dispose();
    }

    public static bool HasNotCompletedJobs(NpgsqlDataSource dataSource)
    {
        using var conn = dataSource.OpenConnection();
        using var cmd = new NpgsqlCommand("SELECT job_name FROM qrtz_job_details LIMIT 1;", conn);
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    public static Task<IScheduler> CreateScheduler()
    {
        return SchedulerBuilder.Create()
            .UseDefaultThreadPool(x => x.MaxConcurrency = 10)
            .UsePersistentStore(x =>
            {
                x.UseClustering();
                x.UseSystemTextJsonSerializer();
                x.UsePostgres(p =>
                {
                    p.ConnectionString = DataSourceFactory.ConnectionString;
                    p.TablePrefix = "qrtz_";
                });

            })
            .BuildScheduler();
    }

    public static Task CreateTestJob(IScheduler scheduler, TestJobParam jobParam)
    {
        var trigger = TriggerBuilder.Create()
            .WithIdentity(Guid.NewGuid().ToString())
            .StartNow()
            .Build();

        var job = JobBuilder.Create<QuartzTestJob>()
            .WithIdentity(Guid.NewGuid().ToString())
            .UsingJobData(nameof(TestJobParam.Id), jobParam.Id)
            .UsingJobData(nameof(TestJobParam.Value), jobParam.Value)
            .UsingJobData(nameof(TestJobParam.DelayMs), jobParam.DelayMs)
            .Build();

        return scheduler.ScheduleJob(job, trigger);
    }
}
