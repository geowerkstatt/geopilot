using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Geopilot.Pipeline.Processes.XtfMetadata;

/// <summary>
/// Reads metadata of a delivery, such as its validation scope, from an INTERLIS 2.4 transfer file in a single pass.
/// </summary>
internal class XtfMetadataExtractorProcess
{
    /// <summary>
    /// How deep an object may nest below its own element, and the file outside the objects. DMAV geometry needs about
    /// ten levels. Deeper nesting from a deliverer would cost quadratic time in LINQ to XML, can overflow the stack,
    /// which ends the whole process, and costs the reader memory for every open level.
    /// </summary>
    internal const int MaxObjectDepth = 64;

    /// <summary>
    /// How many characters one loaded object may hold: its text, its attribute values and <see cref="NodeCost"/> for
    /// every element, attribute and text. The largest object of a DMAV municipality dataset holds well under a million.
    /// </summary>
    internal const int MaxObjectCharacters = 16 * 1024 * 1024;

    /// <summary>
    /// What every element, attribute and text costs against <see cref="MaxObjectCharacters"/> besides its characters,
    /// so that an object of many empty nodes is bounded as well. DMAV geometry still fits about 160,000 coordinates
    /// into one object.
    /// </summary>
    internal const int NodeCost = 16;

    /// <summary>
    /// How many distinct element and attribute names the loaded objects of one file may use. A transfer only uses the
    /// vocabulary of its models, a complete DMAV dataset about 300 names, and a name can outlive the run.
    /// </summary>
    internal const int MaxDistinctNames = 2_000;

    /// <summary>
    /// How long an element or attribute name of a loaded object, and its namespace, may be. DMAV names have up to 29
    /// characters, their namespaces up to 68.
    /// </summary>
    internal const int MaxNameLength = 128;

    /// <summary>
    /// How long a scope may be. A scope is an identifier such as a BFS number or a canton code.
    /// </summary>
    internal const int MaxScopeLength = 255;

    /// <summary>
    /// How many of several matched values the status message lists.
    /// </summary>
    internal const int MaxListedValues = 5;

    private static readonly XNamespace Interlis = XtfObjectPath.Interlis24Namespace;

    private static readonly LocalizedText FoundStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Scope aus der Transferdatei gelesen: {0}." },
        { "fr", "Scope lu dans le fichier de transfert : {0}." },
        { "it", "Scope letto dal file di trasferimento: {0}." },
        { "en", "Scope read from the transfer file: {0}." },
    };

    private static readonly LocalizedText NoMatchStatusMessage = new Dictionary<string, string>
    {
        { "de", "Kein Scope gefunden: Der konfigurierte Pfad trifft in der Transferdatei nichts." },
        { "fr", "Aucun scope trouvé : le chemin configuré ne trouve rien dans le fichier de transfert." },
        { "it", "Nessuno scope trovato: il percorso configurato non trova nulla nel file di trasferimento." },
        { "en", "No scope found: the configured path matches nothing in the transfer file." },
    };

    private static readonly LocalizedText NoValidMatchStatusMessage = new Dictionary<string, string>
    {
        { "de", "Kein Scope gefunden: Keines der Objekte, die der Pfad trifft, ist aktuell gültig (genehmigte Entstehung, kein genehmigter Untergang)." },
        { "fr", "Aucun scope trouvé : aucun des objets trouvés par le chemin n'est actuellement valide (Entstehung approuvée, pas d'Untergang approuvé)." },
        { "it", "Nessuno scope trovato: nessuno degli oggetti trovati dal percorso è attualmente valido (Entstehung approvata, nessun Untergang approvato)." },
        { "en", "No scope found: none of the objects the path matches is currently valid (approved Entstehung, no approved Untergang)." },
    };

    private static readonly LocalizedText AmbiguousStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Kein eindeutiger Scope: Der konfigurierte Pfad trifft mehrere Werte ({0})." },
        { "fr", "Scope ambigu : le chemin configuré trouve plusieurs valeurs ({0})." },
        { "it", "Scope ambiguo: il percorso configurato trova più valori ({0})." },
        { "en", "No unique scope: the configured path matches several values ({0})." },
    };

    private static readonly LocalizedText UnusableValueStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Kein Scope gefunden: Der gelesene Wert taugt nicht als Scope (länger als {0} Zeichen oder mit Steuer- oder unsichtbaren Zeichen)." },
        { "fr", "Aucun scope trouvé : la valeur lue ne peut pas servir de scope (plus de {0} caractères ou caractères de contrôle ou invisibles)." },
        { "it", "Nessuno scope trovato: il valore letto non può servire da scope (più di {0} caratteri o caratteri di controllo o invisibili)." },
        { "en", "No scope found: the value read is unusable as a scope (longer than {0} characters or containing control or invisible characters)." },
    };

    private static readonly LocalizedText UnsupportedStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Datei ist kein INTERLIS-2.4-Transfer, darum werden keine Metadaten gelesen." },
        { "fr", "Le fichier n'est pas un transfert INTERLIS 2.4, aucune métadonnée n'est donc lue." },
        { "it", "Il file non è un trasferimento INTERLIS 2.4, quindi non vengono letti metadati." },
        { "en", "The file is not an INTERLIS 2.4 transfer, so no metadata is read." },
    };

    private static readonly LocalizedText UnreadableStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Die Datei ist kein lesbares XML (Zeile {0}, Position {1})." },
        { "fr", "Le fichier n'est pas un XML lisible (ligne {0}, position {1})." },
        { "it", "Il file non è un XML leggibile (riga {0}, posizione {1})." },
        { "en", "The file is not readable XML (line {0}, position {1})." },
    };

    private static readonly LocalizedText UnreadableStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die Datei ist kein lesbares XML." },
        { "fr", "Le fichier n'est pas un XML lisible." },
        { "it", "Il file non è un XML leggibile." },
        { "en", "The file is not readable XML." },
    };

    private readonly XtfMetadataQuery scope;
    private readonly ILogger logger;

    /// <summary>
    /// Create a new instance of the <see cref="XtfMetadataExtractorProcess"/> class.
    /// </summary>
    /// <param name="scope">Where the validation scope comes from: an XPath applied to every object of the transfer file and the rule for several matches. A definition without it does not start.</param>
    /// <param name="logger">Logger instance for logging messages during the extraction.</param>
    public XtfMetadataExtractorProcess(XtfMetadataQuery scope, ILogger logger)
    {
        this.scope = scope;
        this.logger = logger;
    }

    /// <summary>
    /// Reads the configured metadata from the transfer file.
    /// </summary>
    /// <param name="transferFile">The INTERLIS 2.4 transfer file of the delivery.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The metadata read, how far the file could be read, and a status message.</returns>
    [PipelineProcessRun]
    public async Task<XtfMetadataExtractorResult> RunAsync(IPipelineFile transferFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transferFile);
        cancellationToken.ThrowIfCancellationRequested();

        // A number beyond the three fetch values passes the startup validation, so it is refused here, naming it.
        if (!Enum.IsDefined(scope.Fetch))
            throw new InvalidOperationException($"The fetch value <{scope.Fetch}> of scope is unknown. Use Single, First or CurrentlyValid.");

        var matches = new ScopeMatches(XtfObjectPath.Compile(scope.Path), scope.Fetch);

        logger.LogInformation($"Reading metadata from transfer file <{transferFile.OriginalFileName}>...");

        try
        {
            using var stream = await transferFile.OpenReadAsync(cancellationToken);
            using var reader = XmlReader.Create(stream, CreateReaderSettings());

            await reader.MoveToContentAsync();
            if (!IsInterlis24Transfer(reader))
            {
                logger.LogInformation($"Transfer file <{transferFile.OriginalFileName}> is not an INTERLIS 2.4 transfer.");
                return new XtfMetadataExtractorResult { FileState = XtfFileState.Unsupported, StatusMessage = UnsupportedStatusMessage };
            }

            await foreach (var transferObject in ReadObjectsAsync(reader, isNeeded: matches.Needs, cancellationToken))
            {
                if (matches.Add(transferObject))
                    break;
            }
        }
        catch (XmlException e)
        {
            // The status message names the position only: the parser text is English and addressed to developers.
            logger.LogInformation($"Transfer file <{transferFile.OriginalFileName}> is not readable XML: {e.Message}");
            var statusMessage = e.LineNumber > 0
                ? Format(UnreadableStatusMessageFormat, e.LineNumber, e.LinePosition)
                : UnreadableStatusMessage;
            return new XtfMetadataExtractorResult { FileState = XtfFileState.Unreadable, StatusMessage = statusMessage };
        }

        var extractedScope = matches.ToScope();
        logger.LogInformation($"Metadata of transfer file <{transferFile.OriginalFileName}> read. Scope: <{extractedScope.Value}>.");
        return new XtfMetadataExtractorResult
        {
            Scope = extractedScope.Value,
            FileState = XtfFileState.Readable,
            StatusMessage = extractedScope.StatusMessage,
        };
    }

    /// <summary>
    /// Streams through the data section and yields, loaded, every object <paramref name="isNeeded"/> selects by its
    /// element name; all others are skipped without being loaded. Objects are the direct children of a basket.
    /// </summary>
    private static async IAsyncEnumerable<XElement> ReadObjectsAsync(XmlReader reader, Func<string, string, bool> isNeeded, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chunk = new char[4096];
        var names = new HashSet<(string Namespace, string LocalName)>();
        var inDataSection = false;
        var hasNode = await reader.ReadAsync();
        while (hasNode)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Outside the objects, for example in the header section, the nesting is bounded as well.
            if (reader.Depth > MaxObjectDepth)
                throw Refused(reader, $"The file nests deeper than {MaxObjectDepth} levels.");

            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1)
                inDataSection = reader.LocalName == "datasection" && reader.NamespaceURI == XtfObjectPath.Interlis24Namespace;

            if (inDataSection && reader.NodeType == XmlNodeType.Element && reader.Depth == 3)
            {
                if (isNeeded(reader.LocalName, reader.NamespaceURI))
                    yield return await LoadObjectAsync(reader, chunk, names, cancellationToken);
                else
                    await SkipObjectAsync(reader, cancellationToken);

                // Loading or skipping the object has already moved the reader to the node after it.
                hasNode = !reader.EOF;
                continue;
            }

            hasNode = await reader.ReadAsync();
        }
    }

    /// <summary>
    /// Loads the object the reader stands on and leaves the reader on the node after it, like
    /// <see cref="XNode.ReadFromAsync"/>, but refuses an object that nests deeper than <see cref="MaxObjectDepth"/> or
    /// holds more than <see cref="MaxObjectCharacters"/> characters, reported as unreadable XML.
    /// </summary>
    private static async Task<XElement> LoadObjectAsync(XmlReader reader, char[] chunk, HashSet<(string Namespace, string LocalName)> names, CancellationToken cancellationToken)
    {
        var (root, characters) = NewElement(reader, names);
        if (reader.IsEmptyElement)
        {
            await reader.ReadAsync();
            return root;
        }

        var open = new Stack<XElement>();
        open.Push(root);
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    var (child, childCharacters) = NewElement(reader, names);
                    characters += childCharacters;
                    open.Peek().Add(child);
                    if (!reader.IsEmptyElement)
                        open.Push(child);
                    if (open.Count > MaxObjectDepth)
                        throw Refused(reader, $"An object nests deeper than {MaxObjectDepth} levels.");
                    break;

                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace:
                    var text = await ReadTextAsync(reader, chunk, MaxObjectCharacters - characters);
                    characters += NodeCost + text.Length;
                    open.Peek().Add(new XText(text));
                    break;

                case XmlNodeType.EndElement:
                    open.Pop();
                    if (open.Count == 0)
                    {
                        await reader.ReadAsync();
                        return root;
                    }

                    break;
            }

            if (characters > MaxObjectCharacters)
                throw Refused(reader, $"An object holds more than {MaxObjectCharacters} characters.");
        }

        // Not reached for a file that ends inside an object: the reader reports that itself.
        throw Refused(reader, "The file ends inside an object.");
    }

    /// <summary>
    /// Skips the object the reader stands on and leaves the reader on the node after it, like
    /// <see cref="XmlReader.SkipAsync"/>, but refuses nesting deeper than <see cref="MaxObjectDepth"/>: the reader keeps
    /// state for every open level, so an object that is not loaded costs memory for its depth as well.
    /// </summary>
    private static async Task SkipObjectAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var objectDepth = reader.Depth;
        if (!reader.IsEmptyElement)
        {
            while (await reader.ReadAsync() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == objectDepth))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement && reader.Depth - objectDepth >= MaxObjectDepth)
                    throw Refused(reader, $"An object nests deeper than {MaxObjectDepth} levels.");
            }
        }

        await reader.ReadAsync();
    }

    // Reads a text in chunks, so that an oversized text is refused before it is held in memory as a whole. Most texts
    // fit into one chunk and cost no more than the string itself.
    private static async Task<string> ReadTextAsync(XmlReader reader, char[] chunk, long remainingCharacters)
    {
        var read = await reader.ReadValueChunkAsync(chunk, 0, chunk.Length);
        var first = new string(chunk, 0, read);
        read = await reader.ReadValueChunkAsync(chunk, 0, chunk.Length);
        if (read == 0)
            return first;

        var text = new StringBuilder(first);
        do
        {
            text.Append(chunk, 0, read);
            if (text.Length > remainingCharacters)
                throw Refused(reader, $"An object holds more than {MaxObjectCharacters} characters.");
        }
        while ((read = await reader.ReadValueChunkAsync(chunk, 0, chunk.Length)) > 0);

        return text.ToString();
    }

    // Creates the element the reader stands on with its attributes, and what it costs against MaxObjectCharacters.
    private static (XElement Element, long Characters) NewElement(XmlReader reader, HashSet<(string Namespace, string LocalName)> names)
    {
        var element = new XElement(NameOf(reader, names));
        long characters = NodeCost;

        // shortcut: the reader holds a start tag with all its attributes at once, which a crafted tag drives to about
        // 20 times the upload size; XTF attributes are few short identifiers, so a transfer never comes close.
        while (reader.MoveToNextAttribute())
        {
            // Namespace declarations are already resolved into the names.
            if (reader.NamespaceURI != XNamespace.Xmlns.NamespaceName)
            {
                element.Add(new XAttribute(NameOf(reader, names), reader.Value));
                characters += NodeCost + reader.Value.Length;
            }
        }

        reader.MoveToElement();
        return (element, characters);
    }

    // shortcut: LINQ to XML keeps names while their namespace is in use, the INTERLIS one for good, so names are capped in
    // number and length; a crafted upload still leaves up to 0.7 MB for good, objects built without LINQ to XML none.
    private static XName NameOf(XmlReader reader, HashSet<(string Namespace, string LocalName)> names)
    {
        if (reader.LocalName.Length > MaxNameLength || reader.NamespaceURI.Length > MaxNameLength)
            throw Refused(reader, $"The objects of the file use a name or namespace longer than {MaxNameLength} characters.");
        if (names.Add((reader.NamespaceURI, reader.LocalName)) && names.Count > MaxDistinctNames)
            throw Refused(reader, $"The objects of the file use more than {MaxDistinctNames} distinct names.");
        return XName.Get(reader.LocalName, reader.NamespaceURI);
    }

    // Refused like any other unreadable XML, with the position in the file.
    private static XmlException Refused(XmlReader reader, string message)
    {
        var position = reader as IXmlLineInfo;
        return new XmlException(message, null, position?.LineNumber ?? 0, position?.LinePosition ?? 0);
    }

    private static bool IsInterlis24Transfer(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element
        && reader.LocalName == "transfer"
        && reader.NamespaceURI == XtfObjectPath.Interlis24Namespace;

    // The transfer file comes from the deliverer: no DTD, no external resources.
    // shortcut: the reader keeps every distinct name of the file for the run, skipped objects and header included, which a
    // crafted file drives to about 8 times its size; a counting NameTable in these settings would cap that.
    private static XmlReaderSettings CreateReaderSettings() => new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
    };

    // The value comes from the deliverer and reaches the log, the protocol and the validator: no control characters,
    // and none of the invisible ones that break a line, reverse its direction or hide in plain sight. Checked as whole
    // characters, since some of them lie outside the basic plane.
    private static bool IsUsableScope(string value) =>
        value.Length <= MaxScopeLength && !value.EnumerateRunes().Any(IsInvisible);

    private static bool IsInvisible(Rune character) =>
        Rune.IsControl(character)
        || Rune.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static LocalizedText Format(LocalizedText format, params object[] arguments) =>
        format.Map(message => string.Format(CultureInfo.InvariantCulture, message, arguments));

    // An unusable value is listed as a question mark, further values as an ellipsis.
    private static string ListOf(IReadOnlyList<string> values) =>
        string.Join(", ", values.Take(MaxListedValues).Select(value => IsUsableScope(value) ? value : "?"))
        + (values.Count > MaxListedValues ? ", …" : string.Empty);

    /// <summary>
    /// A value the matches reduce to, or none, with the status message that names it or explains its absence.
    /// </summary>
    private sealed record ExtractedValue(string? Value, LocalizedText StatusMessage);

    /// <summary>
    /// Collects the matches of the scope path while the file streams by and reduces them to the scope once reading is
    /// done. Created per run, so nothing is shared between runs.
    /// </summary>
    private sealed class ScopeMatches
    {
        private readonly XtfObjectPath path;
        private readonly XtfMetadataFetch fetch;

        // shortcut: CurrentlyValid keeps every candidate and every approved id until the end, so a file with very many
        // objects of the class costs memory accordingly; values are cut to what the usability check needs, ids are not.
        private readonly List<Candidate> candidates = new();
        private readonly HashSet<string> approvedIds = new(StringComparer.Ordinal);
        private int valueCount;

        public ScopeMatches(XtfObjectPath path, XtfMetadataFetch fetch)
        {
            this.path = path;
            this.fetch = fetch;
        }

        /// <summary>
        /// Whether an object has to be loaded: one of the class the path names, or any object while approvals are
        /// collected for <see cref="XtfMetadataFetch.CurrentlyValid"/>.
        /// </summary>
        public bool Needs(string localName, string namespaceUri)
        {
            // shortcut: CurrentlyValid loads every object to find the approved Nachführungen, whose class it does not
            // know; scanning only their direct children would be cheaper if very large files become common.
            return fetch == XtfMetadataFetch.CurrentlyValid
                || (localName == path.ObjectName.LocalName && namespaceUri == path.ObjectName.NamespaceName);
        }

        /// <summary>
        /// Adds a loaded object and returns whether reading can stop: for <see cref="XtfMetadataFetch.First"/> after
        /// its match, for <see cref="XtfMetadataFetch.Single"/> once more values are known than the status message lists.
        /// </summary>
        public bool Add(XElement transferObject)
        {
            if (fetch == XtfMetadataFetch.CurrentlyValid)
                NoteApproval(transferObject);

            if (transferObject.Name != path.ObjectName)
                return false;

            var values = path.ValuesOf(transferObject).Select(Shortened).ToList();
            if (values.Count == 0)
                return false;

            candidates.Add(new Candidate(values, ReferenceOf(transferObject, "Entstehung"), ReferenceOf(transferObject, "Untergang")));
            valueCount += values.Count;
            return fetch == XtfMetadataFetch.First || (fetch == XtfMetadataFetch.Single && valueCount > MaxListedValues);
        }

        /// <summary>
        /// Reduces the matches to the scope, or to none with the reason in the status message.
        /// </summary>
        public ExtractedValue ToScope()
        {
            var eligible = fetch == XtfMetadataFetch.CurrentlyValid ? candidates.Where(IsCurrentlyValid).ToList() : candidates;
            var values = eligible.SelectMany(candidate => candidate.Values).ToList();

            if (values.Count == 0)
                return new ExtractedValue(null, candidates.Count > 0 ? NoValidMatchStatusMessage : NoMatchStatusMessage);

            if (values.Count > 1 && fetch != XtfMetadataFetch.First)
                return new ExtractedValue(null, Format(AmbiguousStatusMessageFormat, ListOf(values)));

            var value = values[0];
            return IsUsableScope(value)
                ? new ExtractedValue(value, Format(FoundStatusMessageFormat, value))
                : new ExtractedValue(null, Format(UnusableValueStatusMessageFormat, MaxScopeLength));
        }

        // Keeps one character more than a scope may have, enough to tell that a longer value is unusable.
        private static string Shortened(string value) =>
            value.Length > MaxScopeLength ? value[..(MaxScopeLength + 1)] : value;

        // An object with GenehmigtAm is an approved Nachführung the Entstehung or Untergang of a candidate may reference.
        private void NoteApproval(XElement transferObject)
        {
            var id = transferObject.Attribute(Interlis + "tid")?.Value;
            if (id is not null && transferObject.Element(transferObject.Name.Namespace + "GenehmigtAm") is not null)
                approvedIds.Add(id);
        }

        private bool IsCurrentlyValid(Candidate candidate) =>
            candidate.EntstehungId is not null
            && approvedIds.Contains(candidate.EntstehungId)
            && (candidate.UntergangId is null || !approvedIds.Contains(candidate.UntergangId));

        private static string? ReferenceOf(XElement transferObject, string role) =>
            transferObject.Element(transferObject.Name.Namespace + role)?.Attribute(Interlis + "ref")?.Value;

        private sealed record Candidate(IReadOnlyList<string> Values, string? EntstehungId, string? UntergangId);
    }
}
