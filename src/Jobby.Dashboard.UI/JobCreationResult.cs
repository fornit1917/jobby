namespace Jobby.Dashboard.UI;

// Outcome of a request to create a job: its id, or the reason the server rejected the submitted values.
public sealed record JobCreationResult(Guid? JobId, string? Error);
