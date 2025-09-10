using System.Collections.Concurrent;

namespace Geopilot.Api.Validation;

/// <summary>
/// Stores and retrieves <see cref="ValidationJob"/> instances in memory in a thread-safe manner.
/// </summary>
public class ValidationJobStore : IValidationJobStore
{
    private readonly ConcurrentDictionary<Guid, ValidationJob> jobs = new();

    /// <inheritdoc/>
    public ValidationJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc/>
    public void AddJob(ValidationJob job) => jobs[job.Id] = job;
}
