using Geopilot.PipelineCore.Pipeline;
using System.Xml;

namespace Geopilot.Pipeline.Processes;

/// <summary>
/// Recognizes the <see cref="InterlisVersion"/> of a transfer file, shared by the processes that report it.
/// </summary>
internal static class InterlisVersionDetection
{
    private static readonly Dictionary<string, InterlisVersion> XtfVersionsByNamespace = new()
    {
        { "http://www.interlis.ch/INTERLIS2.3", InterlisVersion.Interlis23 },
        { "http://www.interlis.ch/xtf/2.4/INTERLIS", InterlisVersion.Interlis24 },
    };

    private static ReadOnlySpan<byte> ItfStart => "SCNT"u8;

    /// <summary>
    /// Recognizes the INTERLIS version from the start of a transfer file only: an ITF by its leading <c>SCNT</c>,
    /// an XTF by the namespace of its root element. Returns null for any other file, INTERLIS 2.2 included.
    /// </summary>
    public static async Task<InterlisVersion?> DetectAsync(IPipelineFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (await StartsWithItfHeaderAsync(file, cancellationToken))
                return InterlisVersion.Interlis1;

            var rootNamespace = await ReadRootNamespaceAsync(file, cancellationToken);
            return XtfVersionsByNamespace.TryGetValue(rootNamespace, out var version) ? version : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A file that is not readable as a transfer has no version; a cancelled job must still cancel.
            return null;
        }
    }

    private static async Task<bool> StartsWithItfHeaderAsync(IPipelineFile file, CancellationToken cancellationToken)
    {
        using var stream = await file.OpenReadAsync(cancellationToken);
        var start = new byte[ItfStart.Length];
        var read = await stream.ReadAtLeastAsync(start, start.Length, throwOnEndOfStream: false, cancellationToken);
        return start.AsSpan(0, read).SequenceEqual(ItfStart);
    }

    private static async Task<string> ReadRootNamespaceAsync(IPipelineFile file, CancellationToken cancellationToken)
    {
        using var stream = await file.OpenReadAsync(cancellationToken);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        await reader.MoveToContentAsync();
        return reader.NamespaceURI;
    }
}
