﻿using Jobby.Core.Models;
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
            Cron = reader.GetNullableString("cron"),
            NextJobId = reader.GetNullableGuid("next_job_id"),
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
