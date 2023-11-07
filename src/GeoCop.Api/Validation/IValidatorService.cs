namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Provides methods to schedule validation jobs and access job status information.
    /// </summary>
    public interface IValidatorService
    {
        /// <summary>
        /// Represents the action that performs the validation of a job.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
        /// <returns>The result of the validation as a <see cref="ValidationJobStatus"/>.</returns>
        public delegate Task<ValidationJobStatus> ValidationAction(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously enqueues and executes the <paramref name="action"/> specified.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="action">The action to execute.</param>
        /// <returns></returns>
        Task EnqueueJobAsync(Guid jobId, ValidationAction action);

        /// <summary>
        /// Gets the <see cref="ValidationJobStatus"/> for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns>The <see cref="ValidationJobStatus"/> for the given <paramref name="jobId"/> if the job exists; otherwise, <c>default</c>.</returns>
        ValidationJobStatus? GetJobStatusOrDefault(Guid jobId);
    }
}
