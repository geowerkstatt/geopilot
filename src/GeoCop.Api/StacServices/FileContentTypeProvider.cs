using GeoCop.Api.Models;
using Microsoft.AspNetCore.StaticFiles;
using System.Net.Mime;

namespace GeoCop.Api.StacServices
{
    /// <summary>
    /// Provides access to file content types.
    /// </summary>
    public class FileContentTypeProvider : FileExtensionContentTypeProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileContentTypeProvider"/> class.
        /// </summary>
        public FileContentTypeProvider()
            : base()
        {
            AddOrUpdateMapping(".xtf", "application/interlis+xml");
        }

        /// <summary>
        /// Returns the <see cref="ContentType"/> for the specified <see cref="Asset"/>.
        /// </summary>
        /// <param name="asset"></param>
        /// <returns>The <see cref="ContentType"/>.</returns>
        public ContentType GetContentType(Asset asset)
        {
            return GetContentType(Path.GetExtension(asset.OriginalFilename));
        }

        /// <summary>
        /// Returns the <see cref="ContentType"/> for the specified file extension.
        /// </summary>
        /// <param name="fileExtension"></param>
        /// <returns>The <see cref="ContentType"/>.</returns>
        public ContentType GetContentType(string fileExtension)
        {
            const string DefaultContentType = "application/octet-stream";

            if (!TryGetContentType(fileExtension, out var contentType))
            {
                contentType = DefaultContentType;
            }

            return new ContentType(contentType);
        }

        private void AddOrUpdateMapping(string fileExtension, string contentType)
        {
            if (!Mappings.TryGetValue(fileExtension, out var _))
            {
                Mappings.Add(fileExtension, contentType);
            }
            else
            {
                Mappings[fileExtension] = contentType;
            }
        }
    }
}
