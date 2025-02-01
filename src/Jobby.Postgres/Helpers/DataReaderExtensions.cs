using Jobby.Abstractions.Models;
using System.Data.Common;

namespace Jobby.Postgres.Helpers;

internal static class DataReaderExtensions
{
    public static async Task<JobModel?> GetJobAsync(this DbDataReader reader)
    {
        if (!reader.HasRows)
        {
            return null;
        }
        await reader.ReadAsync();
        var job = new JobModel
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            JobName = reader.GetString(reader.GetOrdinal("job_name")),
            JobParam = reader.GetNullableString("job_param"),
            Status = (JobStatus)reader.GetInt32(reader.GetOrdinal("status")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            ScheduledStartAt = reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
            LastStartedAt = reader.GetNullableDatetime("last_started_at"),
            LastFinishedAt = reader.GetNullableDatetime("last_finished_at"),
            StartedCount = reader.GetInt32(reader.GetOrdinal("started_count")),
            RecurrentJobKey = reader.GetNullableString("recurrent_job_key"),
            Cron = reader.GetNullableString("cron")
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
}
