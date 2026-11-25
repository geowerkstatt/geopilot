using Geopilot.Api.Models;

namespace Geopilot.Api.FileAccess
{
    /// <summary>
    /// Provides functionality to record, delete and download asset files.
    /// </summary>
    public interface IAssetHandler
    {
        /// <summary>
        /// Hashes the job's staged delivery files and returns them as assets. Moves nothing: the files stay in
        /// staging until <see cref="PromoteJobAssets"/>.
        /// </summary>
        /// <param name="jobId">The validation job id.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>List of <see cref="Asset" /> representing the staged delivery files of the job.</returns>
        Task<IEnumerable<Asset>> RecordJobAssetsAsync(Guid jobId, CancellationToken cancellationToken);

        /// <summary>
        /// Moves the staged files of a saved delivery into its delivery directory. Does nothing when nothing is staged.
        /// </summary>
        /// <param name="jobId">The job id of the saved delivery.</param>
        void PromoteJobAssets(Guid jobId);

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
