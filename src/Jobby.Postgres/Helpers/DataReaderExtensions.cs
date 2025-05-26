using Jobby.Core.Models;
using System.Data.Common;

namespace Jobby.Postgres.Helpers;

internal static class DataReaderExtensions
{
    public static async Task<Job?> GetJobAsync(this DbDataReader reader)
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

        var job = new Job
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            JobName = reader.GetString(reader.GetOrdinal("job_name")),
            JobParam = reader.GetNullableString("job_param"),
            Status = (JobStatus)reader.GetInt32(reader.GetOrdinal("status")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            ScheduledStartAt = reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
            LastStartedAt = reader.GetNullableDatetime("last_started_at"),
            LastFinishedAt = reader.GetNullableDatetime("last_finished_at"),
            StartedCount = reader.GetInt32(reader.GetOrdinal("started_count")),
            Cron = reader.GetNullableString("cron"),
            NextJobId = reader.GetNullableGuid("next_job_id"),
            ServerId = reader.GetString(reader.GetOrdinal("server_id")),
            CanBeRestarted = reader.GetBoolean(reader.GetOrdinal("can_be_restarted"))
        };
        return job;
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
