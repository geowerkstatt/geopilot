namespace Geopilot.Api.Validation
{
    /// <summary>
    /// Provides methods to store and retrieve <see cref="ValidationJob"/> instances.
    /// </summary>
    public interface IValidationJobStore
    {
        /// <summary>
        /// Retrieves a <see cref="ValidationJob"/> by its id.
        /// </summary>
        /// <param name="jobId">The id of the validation job.</param>
        /// <returns>The <see cref="ValidationJob"/> with the specified id or <c>null</c> if no job with the specified id exists.</returns>
        /// <returns></returns>
        ValidationJob? GetJob(Guid jobId);

        /// <summary>
        /// Adds a new <see cref="ValidationJob"/> to the store.
        /// </summary>
        /// <param name="job">The validation job to add.</param>
        void AddJob(ValidationJob job);
    }
}
