using System.Threading.Channels;

namespace Geopilot.Api.Validation
{
    /// <summary>
    /// Provides methods to store and retrieve <see cref="ValidationJob"/> instances.
    /// </summary>
    public interface IValidationJobStore
    {
        ChannelReader<IValidator> ValidationQueue { get; }

        /// <summary>
        /// Retrieves a <see cref="ValidationJob"/> by its id.
        /// </summary>
        /// <param name="jobId">The id of the validation job.</param>
        /// <returns>The <see cref="ValidationJob"/> with the specified id or <c>null</c> if no job with the specified id exists.</returns>
        /// <returns></returns>
        ValidationJob? GetJob(Guid jobId);

        Guid? GetJobId(IValidator validator);

        ValidationJob CreateJob();

        bool TryAddFileToJob(Guid jobId, string originalFileName, string tempFileName, out ValidationJob updatedJob);

        bool TryStartJob(Guid jobId, ICollection<IValidator> validators, out ValidationJob updatedJob);

        bool TryAddValidatorResult(IValidator validator, ValidatorResult result, out ValidationJob updatedJob);
    }
}
