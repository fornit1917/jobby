﻿using Npgsql;
using Quartz;

namespace Jobby.Benchmarks.QuartzBenchmarks;

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

    public static Task<IScheduler> CreateScheduler(int maxConcurrency = 10)
    {
        return SchedulerBuilder.Create()
            .UseDefaultThreadPool(x =>
            {
                x.MaxConcurrency = maxConcurrency;
            })
            .WithMaxBatchSize(maxConcurrency)
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

    public static Task CreateTestJob(IScheduler scheduler, QuartzTestJobParam jobParam)
    {
        var trigger = TriggerBuilder.Create()
            .WithIdentity(Guid.NewGuid().ToString())
            .StartNow()
            .Build();

        var job = JobBuilder.Create<QuartzTestJob>()
            .WithIdentity(Guid.NewGuid().ToString())
            .UsingJobData(nameof(QuartzTestJobParam.Id), jobParam.Id)
            .UsingJobData(nameof(QuartzTestJobParam.Value), jobParam.Value)
            .UsingJobData(nameof(QuartzTestJobParam.DelayMs), jobParam.DelayMs)
            .Build();

        return scheduler.ScheduleJob(job, trigger);
    }

    public static Task CreateTestJobs(IScheduler scheduler, IReadOnlyList<QuartzTestJobParam> jobsParams)
    {
        var jobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>();
        for (int i = 0; i < jobsParams.Count; i++)
        {
            var jobParam = jobsParams[i];

            var trigger = TriggerBuilder.Create()
            .WithIdentity(Guid.NewGuid().ToString())
            .StartNow()
            .Build();

            var job = JobBuilder.Create<QuartzTestJob>()
                .WithIdentity(Guid.NewGuid().ToString())
                .UsingJobData(nameof(QuartzTestJobParam.Id), jobParam.Id)
                .UsingJobData(nameof(QuartzTestJobParam.Value), jobParam.Value)
                .UsingJobData(nameof(QuartzTestJobParam.DelayMs), jobParam.DelayMs)
                .Build();

            jobs[job] = [trigger];
        }
        return scheduler.ScheduleJobs(jobs, false);
    }
}
