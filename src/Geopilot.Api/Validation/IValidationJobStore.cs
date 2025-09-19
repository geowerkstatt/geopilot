﻿using System.Threading.Channels;

namespace Geopilot.Api.Validation
{
    /// <summary>
    /// Provides methods to store, retrieve and update <see cref="ValidationJob"/> instances.
    /// </summary>
    public interface IValidationJobStore
    {
        /// <summary>
        /// Gets the channel reader for the validation queue, which provides access to a stream of <see
        /// cref="IValidator"/> instances.
        /// </summary>
        /// <remarks>The validation queue is used to process validation tasks asynchronously. Consumers
        /// can read from the queue using the returned <see cref="ChannelReader{T}"/> to retrieve <see
        /// cref="IValidator"/> instances as they become available.
        /// It is expected that consumers deliver an <see cref="ValidatorResult"/> for each consumed <see cref="IValidator"/>
        /// by calling <see cref="AddValidatorResult(IValidator, ValidatorResult)"/>.</remarks>
        ChannelReader<IValidator> ValidationQueue { get; }

        /// <summary>
        /// Retrieves a <see cref="ValidationJob"/> by its id.
        /// </summary>
        /// <param name="jobId">The id of the validation job.</param>
        /// <returns>The <see cref="ValidationJob"/> with the specified id or <see langword="null"/> if no job with the specified id exists.</returns>
        ValidationJob? GetJob(Guid jobId);

        /// <summary>
        /// Retrieves the id of the <see cref="ValidationJob"/> the specified validator is associated with.
        /// </summary>
        /// <param name="validator">The validator to get the associated job id of.</param>
        /// <returns>The <see cref="Guid"/> of the job associated with the specified validator, or <see langword="null"/> if no job is associated with the specified validator.</returns>
        Guid? GetJobId(IValidator validator);

        /// <summary>
        /// Creates and returns a new instance of a <see cref="ValidationJob"/>.
        /// </summary>
        /// <returns>A <see cref="ValidationJob"/> instance representing the newly created job.</returns>
        ValidationJob CreateJob();

        /// <summary>
        /// Adds the original and temporary file name to the specified job,
        /// signaling that the file for the job has been uploaded and thus updating its status to <see cref="Status.Ready"/>.
        /// </summary>
        /// <remarks>This method only succeeds if the job has the status <see cref="Status.Created"/>, meaning no file has been added yet.</remarks>
        /// <param name="jobId">The id of the job to add the file to.</param>
        /// <param name="originalFileName">The original file name of the uploaded file.</param>
        /// <param name="tempFileName">The temporary, sanitized, internal file name of the uploaded file.</param>
        /// <returns>The updated job with the file names set and its status set to <see cref="Status.Ready"/>.</returns>
        /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
        /// <exception cref="InvalidOperationException">If the status of the job is not <see cref="Status.Created"/>.</exception>
        ValidationJob AddFileToJob(Guid jobId, string originalFileName, string tempFileName);

        /// <summary>
        /// Starts the validation job with the specified <paramref name="jobId"/> using the specified <paramref name="validators"/>.
        /// </summary>
        /// <remarks>This method only succeeds if the job has the status <see cref="Status.Ready"/>, meaning a file has been added and the job is ready to be processed.</remarks>
        /// <param name="jobId">The id of the job to start.</param>
        /// <param name="validators">A collection of validators, that are already configured for the job. Cannot be null or empty.</param>
        /// <returns>the updated validation job with its status set to <see cref="Status.Ready"/>.</returns>
        /// <exception cref="ArgumentException">If no job with the <paramref name="jobId"/> was found.</exception>
        /// <exception cref="ArgumentException">If <paramref name="validators"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">If the status of the job is not <see cref="Status.Ready"/>.</exception>
        ValidationJob StartJob(Guid jobId, ICollection<IValidator> validators);

        /// <summary>
        /// Adds the <paramref name="result"/> to the job associated with the specified <paramref name="validator"/>.
        /// </summary>
        /// <remarks>This method only succeeds if the job has the status <see cref="Status.Processing"/>, meaning the job has been started and is currently being processed.</remarks>
        /// <param name="validator">The validator that produced the result and is associated with a job.</param>
        /// <param name="result">The result produced by the validator.</param>
        /// <returns>The updated job with the result added.</returns>
        /// <exception cref="ArgumentException">If <paramref name="validator"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="result"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">If the specified <paramref name="validator"/> is not associated with any job.</exception>
        /// <exception cref="InvalidOperationException">If the status of the job is not <see cref="Status.Processing"/>.</exception>
        ValidationJob AddValidatorResult(IValidator validator, ValidatorResult result);
    }
}
