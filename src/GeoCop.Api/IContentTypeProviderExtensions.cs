using GeoCop.Api.Models;
using Microsoft.AspNetCore.StaticFiles;
using System.Net.Mime;

namespace GeoCop.Api;

/// <summary>
/// Provides access to file content types.
/// </summary>
public static class IContentTypeProviderExtensions
{
    private const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// Returns the <see cref="ContentType"/> for the specified <see cref="Asset"/>.
    /// </summary>
    /// <param name="contentTypeProvider">The IContentTypeProvider to extend.</param>
    /// <param name="asset">The asset from which the content type should be read.</param>
    /// <returns>The <see cref="ContentType"/>.</returns>
    public static ContentType GetContentType(this IContentTypeProvider contentTypeProvider, Asset asset)
    {
        return contentTypeProvider.GetContentType(asset.OriginalFilename);
    }

    /// <summary>
    /// Returns the <see cref="ContentType"/> for the specified file extension.
    /// </summary>
    /// <param name="contentTypeProvider">The IContentTypeProvider to extend.</param>
    /// <param name="fileName">The file from which the content type should be read.</param>
    /// <returns>The <see cref="ContentType"/>.</returns>
    public static ContentType GetContentType(this IContentTypeProvider contentTypeProvider, string fileName)
    {
        return new ContentType(contentTypeProvider.GetContentTypeAsString(fileName));
    }

    /// <summary>
    /// Returns the <see cref="ContentType"/> for the specified file extension.
    /// </summary>
    /// <param name="contentTypeProvider">The IContentTypeProvider to extend.</param>
    /// <param name="fileName">The file from which the content type should be read.</param>
    /// <returns>The content type as string.</returns>
    public static string GetContentTypeAsString(this IContentTypeProvider contentTypeProvider, string fileName)
    {
        if (!contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            contentType = DefaultContentType;
        }

        return contentType;
    }
}
