namespace GeoCop.Api.FileAccess
{
    /// <summary>
    /// Downloads files from the persistent storage.
    /// </summary>
    public interface IPersistedAssetDownload
    {
        /// <summary>
        /// Downloads an asset from the persistent storage.
        /// </summary>
        /// <returns>The asset as a <see cref="File"/>.</returns>
        Task<(byte[], string)> DownloadAssetAsync(Guid jobId, string assetName);
    }
}
