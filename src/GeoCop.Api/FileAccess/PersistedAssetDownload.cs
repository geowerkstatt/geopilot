using Microsoft.AspNetCore.StaticFiles;

namespace GeoCop.Api.FileAccess
{
    /// <summary>
    /// Downloads files from the persistent storage.
    /// </summary>
    public class PersistedAssetDownload : IPersistedAssetDownload
    {
        private readonly ILogger<PersistedAssetDownload> logger;
        private readonly IDirectoryProvider directoryProvider;
        private readonly IContentTypeProvider fileContentTypeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistedAssetDownload"/> class.
        /// </summary>
        public PersistedAssetDownload(ILogger<PersistedAssetDownload> logger, IDirectoryProvider directoryProvider, IContentTypeProvider fileContentTypeProvider)
        {
            this.logger = logger;
            this.directoryProvider = directoryProvider;
            this.fileContentTypeProvider = fileContentTypeProvider;
        }

        /// <inheritdoc/>
        public async Task<(byte[], string)> DownloadAssetAsync(Guid jobId, string assetName)
        {
            try
            {
                var filePath = Path.Combine(directoryProvider.GetAssetDirectoryPath(jobId), assetName);
                if (!File.Exists(filePath)) throw new FileNotFoundException($"File {filePath} not found.");
                var stream = await File.ReadAllBytesAsync(filePath);
                return (stream, fileContentTypeProvider.GetContentTypeAsString(assetName));
            }
            catch (Exception e)
            {
                var message = $"Failed to download asset <{assetName}>.";
                logger.LogError(e, message);
                throw new InvalidOperationException(message, e);
            }
        }
    }
}
