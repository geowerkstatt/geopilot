using System.Collections.Concurrent;

namespace Geopilot.Api.Validation
{
    public class ValidationManager : IValidationManager
    {
        private readonly ConcurrentDictionary<Guid, ValidationJob> jobs = new();

        public ValidationJob? GetJob(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

        public void AddJob(ValidationJob job) => jobs[job.Id] = job;
    }
}
