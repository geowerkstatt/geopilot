using GeoCop.Api.Models;

namespace GeoCop.Api
{
    /// <summary>
    /// Migrates files deliverd for validation into a persistent storage.
    /// </summary>
    public interface IValidationAssetPersistor
    {
        /// <summary>
        /// Migrates all log files for a validation job into a persistent storage.
        /// </summary>
        /// <param name="jobId">The validation job id. </param>
        /// <returns>List of Assets representing the validation job assets in persistent storage.</returns>
        IEnumerable<Asset> PersistValidationJobAssets(Guid jobId);
    }
}
