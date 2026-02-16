using Jobby.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jobby.IntegrationTests.Postgres.Helpers;

[Table("jobby_jobs")]
internal class JobDbModel
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("job_name")]
    public string JobName { get; set; } = string.Empty;

    [Column("cron")]
    public string? Cron { get; set; }

    [Column("job_param")]
    public string? JobParam { get; set; }

    [Column("status")]
    public JobStatus Status { get; set; }

    [Column("error")]
    public string? Error { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("scheduled_start_at")]
    public DateTime ScheduledStartAt { get; set; }

    [Column("last_started_at")]
    public DateTime? LastStartedAt { get; set; }

    [Column("last_finished_at")]
    public DateTime? LastFinishedAt { get; set; }

    [Column("started_count")]
    public int StartedCount { get; set; }

    [Column("next_job_id")]
    public Guid? NextJobId { get; set; }

    [Column("server_id")]
    public string? ServerId { get; set; }

    [Column("can_be_restarted")]
    public bool CanBeRestarted { get; set; }
    
    [Column("queue_name")]
    public string QueueName { get; set; } = QueueSettings.DefaultQueueName;
    
    [Column("serializable_group_id")]
    public string? SerializableGroupId { get; set; }
    
    [Column("lock_group_if_failed")]
    public bool LockGroupIfFailed { get; set; }

    [Column("is_exclusive")]
    public bool IsExclusive { get; set; }

    public JobExecutionModel ToJobExecutionModel()
    {
        return new JobExecutionModel
        {
            Id = Id,
            NextJobId = NextJobId,
            ServerId = ServerId ?? string.Empty,
            Cron = Cron,
            JobName = JobName,
            JobParam = JobParam,
            StartedCount = StartedCount,
        };
    }
}
