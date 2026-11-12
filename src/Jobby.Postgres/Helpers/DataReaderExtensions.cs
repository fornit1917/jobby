using Jobby.Core.Models;
using System.Data.Common;

namespace Jobby.Postgres.Helpers;

internal static class DataReaderExtensions
{
    public static async Task<JobExecutionModel?> GetJobAsync(this DbDataReader reader)
    {
        if (!reader.HasRows)
        {
            return null;
        }

        var hasRow = await reader.ReadAsync();
        if (!hasRow)
        {
            return null;
        }

        var job = new JobExecutionModel
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            JobName = reader.GetString(reader.GetOrdinal("job_name")),
            JobParam = reader.GetNullableString("job_param"),
            StartedCount = reader.GetInt32(reader.GetOrdinal("started_count")),
            Schedule = reader.GetNullableString("schedule"),
            NextJobId = reader.GetNullableGuid("next_job_id"),
            ServerId =  reader.GetString(reader.GetOrdinal("server_id")),
            ScheduledStartAt = reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
            SchedulerType = reader.GetNullableString("scheduler_type"),
            Traceparent = reader.GetNullableString("traceparent"),
            TracingMode = reader.GetTracingMode("tracing_mode"),
        };
        return job;
    }

    public static async Task<StuckJobModel?> GetStuckJobAsync(this DbDataReader reader)
    {
        if (!reader.HasRows)
        {
            return null;
        }

        var hasRow = await reader.ReadAsync();
        if (!hasRow)
        {
            return null;
        }

        var job = new StuckJobModel
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            JobName = reader.GetString(reader.GetOrdinal("job_name")),
            CanBeRestarted = reader.GetBoolean(reader.GetOrdinal("can_be_restarted")),
            ServerId = reader.GetString(reader.GetOrdinal("server_id"))
        };
        return job;
    }

    /// <summary>
    /// Reads tracing_mode as int and maps it to <see cref="JobbyTracingMode"/>.
    /// Unknown values fall back to <see cref="JobbyTracingMode.CreateNewTraceId"/>
    /// so that a newer DB can never crash an older reader.
    /// </summary>
    public static JobbyTracingMode GetTracingMode(this DbDataReader reader, string columnName)
    {
        var value = reader.GetInt32(reader.GetOrdinal(columnName));
        return Enum.IsDefined(typeof(JobbyTracingMode), value)
            ? (JobbyTracingMode)value
            : JobbyTracingMode.CreateNewTraceId;
    }

    public static string? GetNullableString(this DbDataReader reader, string columnName)
    {
        var index = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(index))
        {
            return null;
        }
        return reader.GetString(index);
    }

    public static DateTime? GetNullableDatetime(this DbDataReader reader, string columnName)
    {
        var index = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(index))
        {
            return null;
        }
        return reader.GetDateTime(index);
    }

    public static Guid? GetNullableGuid(this DbDataReader reader, string columnName)
    {
        var index = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(index))
        {
            return null;
        }
        return reader.GetGuid(index);
    }
}
