namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Provides methods to start validation jobs and access job status information.
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Creates a new <see cref="ValidationJob"/>.
        /// </summary>
        /// <param name="originalFileName">Name of the uploaded file.</param>
        /// <returns>The created <see cref="ValidationJob"/> and a <see cref="FileHandle"/> to store the file to validate.</returns>
        (ValidationJob ValidationJob, FileHandle FileHandle) CreateValidationJob(string originalFileName);

        /// <summary>
        /// Starts the validation job asynchronously.
        /// </summary>
        /// <param name="validationJob">The validation job to start.</param>
        /// <returns>Current job status information.</returns>
        Task<ValidationJobStatus> StartValidationJobAsync(ValidationJob validationJob);

        /// <summary>
        /// Gets the validation job status.
        /// </summary>
        /// <param name="jobId">The id of the validation job.</param>
        /// <returns>Current job status information.</returns>
        ValidationJobStatus? GetJobStatus(Guid jobId);
    }
}
