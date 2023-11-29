using GeoCop.Api.Models;

namespace GeoCop.Api.FileAccess
{
    /// <summary>
    /// Provides functionality to move, delete and download asset files.
    /// </summary>
    public interface IAssetHandler
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
        /// <param name="jobId">The given job id.</param>
        void DeleteJobAssets(Guid jobId);

        /// <summary>
        /// Downloads an asset from the persistent storage.
        /// </summary>
        /// <param name="jobId">The given job id.</param>
        /// <param name="assetName">The sanitized file name.</param>
        /// <returns>The asset as a <see cref="File"/>.</returns>
        Task<(byte[], string)> DownloadAssetAsync(Guid jobId, string assetName);
    }
}
