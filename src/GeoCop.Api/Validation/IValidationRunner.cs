namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Runs validation jobs and provides access job status information.
    /// </summary>
    public interface IValidationRunner
    {
        /// <summary>
        /// Asynchronously enqueues and executes the <paramref name="validators"/> specified.
        /// </summary>
        /// <param name="validationJob">The validation job.</param>
        /// <param name="validators">The validators used to validate the <paramref name="validationJob"/>.</param>
        /// <returns></returns>
        Task EnqueueJobAsync(ValidationJob validationJob, IEnumerable<IValidator> validators);

        /// <summary>
        /// Gets the <see cref="ValidationJob"/> for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns>The <see cref="ValidationJob"/> for the given <paramref name="jobId"/> if the job exists; otherwise, <c>null</c>.</returns>
        ValidationJob? GetJob(Guid jobId);

        /// <summary>
        /// Gets the <see cref="ValidationJobStatus"/> for the given <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns>The <see cref="ValidationJobStatus"/> for the given <paramref name="jobId"/> if the job exists; otherwise, <c>null</c>.</returns>
        ValidationJobStatus? GetJobStatus(Guid jobId);
    }
}
