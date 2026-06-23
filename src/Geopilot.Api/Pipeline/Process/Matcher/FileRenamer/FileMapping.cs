namespace Geopilot.Api.Pipeline.Process.Matcher.FileRenamer;

/// <summary>
/// Configuration parameter for the <see cref="FileRenamerProcess"/>.
/// </summary>
/// <param name="Pattern">The regex pattern to match against the original file name.</param>
/// <param name="Target">The replacement for the matched pattern.</param>
public record class FileMapping(string Pattern, string Target);
