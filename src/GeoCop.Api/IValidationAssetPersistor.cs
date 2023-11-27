using GeoCop.Api.Models;

namespace GeoCop.Api
{
    /// <summary>
    /// Migrates files delivered for validation into a persistent storage.
    /// </summary>
    public interface IValidationAssetPersistor
    {
        /// <summary>
        /// Migrates all log files for a validation job into a persistent storage.
        /// </summary>
        /// <param name="jobId">The validation job id.</param>
        /// <returns>List of <see cref="Asset" /> representing the validation job assets in persistent storage.</returns>
        IEnumerable<Asset> PersistJobAssets(Guid jobId);

        /// <summary>
        /// Deletes all log files for a validation job from persistent storage.
        /// </summary>
        /// <param name="jobId"></param>
        void DeleteJobAssets(Guid jobId);
    }
}
