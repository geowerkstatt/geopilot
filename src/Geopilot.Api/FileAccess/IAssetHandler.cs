using Geopilot.Api.Models;

namespace Geopilot.Api.FileAccess
{
    /// <summary>
    /// Provides functionality to record, delete and download asset files.
    /// </summary>
    public interface IAssetHandler
    {
        /// <summary>
        /// Records the job assets to be persisted.
        /// </summary>
        /// <param name="jobId">The validation job id.</param>
        /// <returns>List of <see cref="Asset" /> representing the validation job assets in persistent storage.</returns>
        IEnumerable<Asset> PersistJobAssets(Guid jobId);

        /// <summary>
        /// Deletes all job assets from persistent storage.
        /// </summary>
        /// <param name="jobId">The given job id.</param>
        void DeleteJobAssets(Guid jobId);

        /// <summary>
        /// Downloads an asset from the persistent storage.
        /// </summary>
        /// <param name="jobId">The given job id.</param>
        /// <param name="assetName">The sanitized file name.</param>
        /// <returns>The asset as a <see cref="File"/>.</returns>
        Task<(byte[] FileStream, string ContentType)> DownloadAssetAsync(Guid jobId, string assetName);
    }
}
