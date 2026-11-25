using System.Text;
using System.Xml;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Writes the reference data mapping as a transfer file of the INTERLIS model <c>IliVRefData_V1_0</c>, the format
/// ilivalidator reads through <c>--refmapping</c>.
/// </summary>
internal static class RefMappingXtfWriter
{
    private const string Model = "IliVRefData_V1_0";
    private const string IliPrefix = "ili";
    private const string InterlisNamespace = "http://www.interlis.ch/xtf/2.4/INTERLIS";
    private const string ModelNamespace = "http://www.interlis.ch/xtf/2.4/IliVRefData_V1_0";

    /// <summary>
    /// Writes one <c>RefData</c> object per entry into a single basket.
    /// </summary>
    public static async Task WriteAsync(IReadOnlyList<RefMappingEntry> entries, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlWriterSettings { Async = true, Indent = true, Encoding = new UTF8Encoding(false) };
        await using var writer = XmlWriter.Create(stream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(IliPrefix, "transfer", InterlisNamespace);

        await writer.WriteStartElementAsync(IliPrefix, "headersection", InterlisNamespace);
        await writer.WriteStartElementAsync(IliPrefix, "models", InterlisNamespace);
        await writer.WriteElementStringAsync(IliPrefix, "model", InterlisNamespace, Model);
        await writer.WriteEndElementAsync();
        await writer.WriteElementStringAsync(IliPrefix, "sender", InterlisNamespace, "geopilot");
        await writer.WriteEndElementAsync();

        await writer.WriteStartElementAsync(IliPrefix, "datasection", InterlisNamespace);
        await writer.WriteStartElementAsync(Model, "Mapping", ModelNamespace);
        await writer.WriteAttributeStringAsync(IliPrefix, "bid", InterlisNamespace, Guid.NewGuid().ToString());

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteEntryAsync(writer, entries[i], $"r{i + 1}");
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndElementAsync();
        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private static async Task WriteEntryAsync(XmlWriter writer, RefMappingEntry entry, string tid)
    {
        await writer.WriteStartElementAsync(Model, "RefData", ModelNamespace);
        await writer.WriteAttributeStringAsync(IliPrefix, "tid", InterlisNamespace, tid);
        await writer.WriteElementStringAsync(Model, "scope", ModelNamespace, entry.Scope);

        // XTF 2.4 encodes a BAG OF a simple type as the attribute element repeated once per value.
        foreach (var refDataUri in entry.RefDataUris)
        {
            await writer.WriteElementStringAsync(Model, "refdata", ModelNamespace, refDataUri);
        }

        if (!string.IsNullOrWhiteSpace(entry.Comment))
        {
            await writer.WriteElementStringAsync(Model, "comment", ModelNamespace, entry.Comment);
        }

        await writer.WriteEndElementAsync();
    }
}
