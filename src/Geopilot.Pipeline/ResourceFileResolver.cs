namespace Geopilot.Pipeline;

/// <summary>
/// Resolves the relative path of a <c>${file(path)}</c> reference to an absolute path under the
/// configured resources root, refusing any path that escapes the root. The path is already confined
/// syntactically at compile time; this is the defense-in-depth check performed once the root is known.
/// </summary>
internal static class ResourceFileResolver
{
    /// <summary>
    /// Resolves <paramref name="relativePath"/> to an absolute path under <paramref name="resourcesRoot"/>.
    /// </summary>
    /// <param name="resourcesRoot">The configured resources root directory.</param>
    /// <param name="relativePath">The path relative to the root, as written in the definition.</param>
    /// <returns>The absolute path of the resource inside the root.</returns>
    /// <exception cref="PipelineRunException">The root is not configured or the path escapes it.</exception>
    internal static string ResolveFullPath(string resourcesRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(resourcesRoot))
            throw new PipelineRunException($"Cannot resolve file '{relativePath}' because no resources directory is configured.");

        var rootFullPath = Path.GetFullPath(resourcesRoot);
        var resolvedFullPath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));

        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        if (!resolvedFullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new PipelineRunException($"File '{relativePath}' resolves outside the resources directory.");

        return resolvedFullPath;
    }
}
