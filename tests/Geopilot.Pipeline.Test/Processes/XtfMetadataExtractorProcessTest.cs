using Geopilot.Pipeline.Processes.XtfMetadata;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfMetadataExtractorProcessTest
{
    private const string BfsNumberPath = "DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer";
    private const string WithoutUntergangPath = "DMAV_HoheitsgrenzenAV_V1_0:Gemeinde[not(DMAV_HoheitsgrenzenAV_V1_0:Untergang)]/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer";
    private const string MunicipalityTid = "ace2ab89-03d6-4280-8038-7f806ad81a46";
    private const string BrokenMunicipality = "<D:Gemeinde ili:tid=\"broken\"><D:BFSNummer>999</D:Name></D:Gemeinde>";

    private static readonly string UnusableValueMessage = $"No scope found: the value read is unusable as a scope (longer than {XtfMetadataExtractorProcess.MaxScopeLength} characters or containing control or invisible characters).";

    private string temporaryDirectory;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void CreateTemporaryDirectory() => temporaryDirectory = Directory.CreateTempSubdirectory("XtfMetadataTest_").FullName;

    [TestCleanup]
    public void DeleteTemporaryDirectory() => Directory.Delete(temporaryDirectory, true);

    [TestMethod]
    public async Task SingleReadsTheBfsNumberOfTheMunicipality()
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken);

        Assert.AreEqual("7901", result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
        Assert.AreEqual("Scope read from the transfer file: 7901.", result.StatusMessage["en"]);
        Assert.AreEqual("Scope aus der Transferdatei gelesen: 7901.", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task AReferenceNamedLikeTheClassIsNotAnObject()
    {
        // The municipality boundary refers to its municipality through an element named Gemeinde as well. Read as an
        // object, the attribute path would match its ili:ref too, and the scope would be ambiguous.
        var result = await Extractor("DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/@*").RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken);

        Assert.AreEqual(MunicipalityTid, result.Scope);
    }

    [TestMethod]
    public async Task AnIliPrefixedAttributePathReadsTheTid()
    {
        var result = await Extractor("DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/@ili:tid").RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken);

        Assert.AreEqual(MunicipalityTid, result.Scope);
    }

    [TestMethod]
    public async Task AClassWrittenWithItsTopicIsAddressable()
    {
        // DMAV writes some classes with their topic, such as DMAV_Bodenbedeckung_V1_0:Bodenbedeckung.Bodenbedeckung.
        var file = Transfer("<D:Bodenbedeckung.Bodenbedeckung ili:tid=\"b1\"><D:Art>449</D:Art></D:Bodenbedeckung.Bodenbedeckung>");

        var result = await Extractor("DMAV_HoheitsgrenzenAV_V1_0:Bodenbedeckung.Bodenbedeckung/DMAV_HoheitsgrenzenAV_V1_0:Art").RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual("449", result.Scope);
    }

    [TestMethod]
    [DataRow("4<![CDATA[4]]>9", DisplayName = "CDATA")]
    [DataRow("4<D:b>4</D:b>9", DisplayName = "Mixed content")]
    public async Task TheValueJoinsAllTextOfTheSelectedElement(string bfsNumber)
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", bfsNumber)), TestContext.CancellationToken);

        Assert.AreEqual("449", result.Scope);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ASelfClosingObjectIsReadPast()
    {
        // Should the reader stay on the empty object, the read loop would load it again forever.
        var file = Transfer("<D:Gemeinde ili:tid=\"g0\"/>" + Gemeinde("g1", "449"));

        var result = await Extractor(BfsNumberPath).RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual("449", result.Scope);
    }

    [TestMethod]
    public async Task SingleLeavesTheScopeEmptyForSeveralMatches()
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Fixture("XtfMetadata/dmav_two_municipalities.xtf"), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
        Assert.AreEqual("No unique scope: the configured path matches several values (7901, 7902).", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task SingleStopsReadingOnceMoreValuesAreKnownThanItLists()
    {
        // One value more than the list shows settles the answer and fills the list; with no more than the list shows,
        // Single still has to read on and meets the break.
        var listed = XtfMetadataExtractorProcess.MaxListedValues;
        var beyondTheList = await Extractor(BfsNumberPath).RunAsync(Transfer(Municipalities(listed + 1) + BrokenMunicipality), TestContext.CancellationToken);
        var withinTheList = await Extractor(BfsNumberPath).RunAsync(Transfer(Municipalities(listed) + BrokenMunicipality), TestContext.CancellationToken);

        var listedValues = string.Join(", ", Enumerable.Range(1, listed).Select(number => number.ToString(CultureInfo.InvariantCulture)));
        Assert.AreEqual(XtfFileState.Readable, beyondTheList.FileState);
        Assert.AreEqual($"No unique scope: the configured path matches several values ({listedValues}, …).", beyondTheList.StatusMessage["en"]);
        Assert.AreEqual(XtfFileState.Unreadable, withinTheList.FileState);
    }

    [TestMethod]
    public async Task AnUnusableValueIsListedAsAQuestionMark()
    {
        var file = Transfer(Gemeinde("g1", "449") + Gemeinde("g2", "44&#10;9"));

        var result = await Extractor(BfsNumberPath).RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual("No unique scope: the configured path matches several values (449, ?).", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task FirstTakesTheFirstMatch()
    {
        var result = await Extractor(BfsNumberPath, XtfMetadataFetch.First).RunAsync(Fixture("XtfMetadata/dmav_two_municipalities.xtf"), TestContext.CancellationToken);

        Assert.AreEqual("7901", result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
    }

    [TestMethod]
    public async Task FirstStopsReadingAfterItsMatch()
    {
        // Broken after the first municipality: First never gets there, while Single has to and finds the file unreadable.
        var file = Transfer(Gemeinde("g1", "449") + BrokenMunicipality);

        var first = await Extractor(BfsNumberPath, XtfMetadataFetch.First).RunAsync(file, TestContext.CancellationToken);
        var single = await Extractor(BfsNumberPath).RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual("449", first.Scope);
        Assert.AreEqual(XtfFileState.Readable, first.FileState);
        Assert.AreEqual(XtfFileState.Unreadable, single.FileState);
    }

    [TestMethod]
    public async Task NoMatchLeavesTheScopeEmpty()
    {
        var path = "DMAV_HoheitsgrenzenAV_V1_0:Gemeinde[DMAV_HoheitsgrenzenAV_V1_0:Fiktiv='true']/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer";

        var result = await Extractor(path).RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
        Assert.AreEqual("No scope found: the configured path matches nothing in the transfer file.", result.StatusMessage["en"]);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("   ", DisplayName = "Only whitespace")]
    public async Task AnEmptyValueIsNoMatch(string bfsNumber)
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", bfsNumber)), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual("No scope found: the configured path matches nothing in the transfer file.", result.StatusMessage["en"]);
    }

    [TestMethod]
    [DataRow("44&#10;9", DisplayName = "Line feed")]
    [DataRow("449&#x2028;INFO forged", DisplayName = "Line separator")]
    [DataRow("&#x202E;944", DisplayName = "Right-to-left override")]
    [DataRow("449&#xE0041;&#xE0042;", DisplayName = "Invisible tag characters outside the basic plane")]
    public async Task AValueWithAnInvisibleCharacterIsUnusable(string bfsNumber)
    {
        // Such characters would reach the log and the protocol as a forged line or as text shown reversed.
        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", bfsNumber)), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
        Assert.AreEqual(UnusableValueMessage, result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task AnOverlongValueIsUnusable()
    {
        var overlong = new string('4', XtfMetadataExtractorProcess.MaxScopeLength + 1);

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", overlong)), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(UnusableValueMessage, result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task TheModelRuleTakesTheNewMunicipalityAfterAnApprovedMerger()
    {
        var result = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(Fixture("XtfMetadata/dmav_completed_merger.xtf"), TestContext.CancellationToken);

        Assert.AreEqual("7910", result.Scope);
    }

    [TestMethod]
    public async Task TheRuleWithoutUntergangTakesTheNewMunicipalityAfterAnApprovedMerger()
    {
        var result = await Extractor(WithoutUntergangPath).RunAsync(Fixture("XtfMetadata/dmav_completed_merger.xtf"), TestContext.CancellationToken);

        Assert.AreEqual("7910", result.Scope);
    }

    [TestMethod]
    public async Task TheRulesDifferWhileAMergerAwaitsApproval()
    {
        // The model still counts the old municipality as valid while its Untergang is not approved, and not yet the
        // new one; the rule that only looks for a missing Untergang already takes the new one.
        var byModel = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(Fixture("XtfMetadata/dmav_pending_merger.xtf"), TestContext.CancellationToken);
        var withoutUntergang = await Extractor(WithoutUntergangPath).RunAsync(Fixture("XtfMetadata/dmav_pending_merger.xtf"), TestContext.CancellationToken);

        Assert.AreEqual("7901", byModel.Scope);
        Assert.AreEqual("7910", withoutUntergang.Scope);
    }

    [TestMethod]
    public async Task CurrentlyValidLeavesTheScopeEmptyForSeveralValidMunicipalities()
    {
        var result = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(Fixture("XtfMetadata/dmav_two_municipalities.xtf"), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual("No unique scope: the configured path matches several values (7901, 7902).", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task CurrentlyValidCountsANachfuehrungOutsideTheFileAsNotApproved()
    {
        var result = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(Fixture("XtfMetadata/dmav_missing_nachfuehrung.xtf"), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Readable, result.FileState);
        Assert.AreEqual("No scope found: none of the objects the path matches is currently valid (approved Entstehung, no approved Untergang).", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task CurrentlyValidKeepsAMunicipalityWhoseUntergangIsMissingFromTheFile()
    {
        var file = Transfer(Nachfuehrung("n1", approved: true) + Gemeinde("g1", "449", entstehung: "n1", untergang: "n9"));

        var result = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual("449", result.Scope);
    }

    [TestMethod]
    [DataRow("UploadFiles/iseltwald_gwp_be13_1.xtf", DisplayName = "INTERLIS 2.3 transfer")]
    [DataRow("XtfMetadata/not_a_transfer.xml", DisplayName = "XML that is not a transfer")]
    public async Task AFileThatIsNotAnInterlis24TransferIsUnsupported(string file)
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Fixture(file), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Unsupported, result.FileState);
        Assert.AreEqual("The file is not an INTERLIS 2.4 transfer, so no metadata is read.", result.StatusMessage["en"]);
    }

    [TestMethod]
    [DataRow("XtfMetadata/dmav_truncated.xtf", DisplayName = "Aborted upload")]
    [DataRow("UploadFiles/helloWorld.pdf", DisplayName = "PDF instead of XTF")]
    public async Task AFileThatIsNotReadableXmlIsUnreadable(string file)
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Fixture(file), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
        Assert.Contains("The file is not readable XML (line ", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task AnEmptyFileIsUnreadableWithoutAPosition()
    {
        var result = await Extractor(BfsNumberPath).RunAsync(WriteFile(string.Empty), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
        Assert.AreEqual("The file is not readable XML.", result.StatusMessage["en"]);
    }

    [TestMethod]
    public async Task ADocumentTypeDefinitionIsRefused()
    {
        // The transfer file comes from the deliverer, so a DTD is not processed; its entity would otherwise read as 449.
        var result = await Extractor(BfsNumberPath).RunAsync(Fixture("XtfMetadata/with_dtd.xtf"), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task ADeeplyNestedObjectIsUnreadable()
    {
        // Such nesting from a deliverer would cost LINQ to XML quadratic time and overflow the stack, ending the process.
        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", Nested(100_000))), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task ADeeplyNestedSkippedObjectIsUnreadable()
    {
        // The reader keeps state for every open level, so an object that is only skipped is bounded as well.
        var boundary = $"<D:Gemeindegrenze ili:tid=\"gg1\">{Nested(100_000)}</D:Gemeindegrenze>";

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", "449") + boundary), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task ADeeplyNestedHeaderIsUnreadable()
    {
        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", "449"), header: Nested(100_000)), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task AnObjectBeyondTheSizeLimitIsUnreadable()
    {
        var oversized = new string('4', XtfMetadataExtractorProcess.MaxObjectCharacters + 1);

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", oversized)), TestContext.CancellationToken);

        Assert.IsNull(result.Scope);
        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task AnObjectOfManyEmptyElementsIsUnreadable()
    {
        // Every element costs against the size limit, otherwise an object of empty elements would be unbounded.
        var elements = (XtfMetadataExtractorProcess.MaxObjectCharacters / XtfMetadataExtractorProcess.NodeCost) + 1;
        var empty = string.Concat(Enumerable.Repeat("<D:x/>", elements));

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", empty)), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task AnObjectOfManyEmptyAttributesIsUnreadable()
    {
        // Every attribute costs against the size limit, however empty it is.
        var attributes = string.Concat(Enumerable.Range(1, 50).Select(number => $" a{number}=\"\""));
        var elements = string.Concat(Enumerable.Repeat($"<D:x{attributes}/>", 25_000));

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", elements)), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task AnObjectOfManyEmptyTextsIsUnreadable()
    {
        // Every text costs against the size limit as well, an empty CDATA section included.
        var sections = (XtfMetadataExtractorProcess.MaxObjectCharacters / XtfMetadataExtractorProcess.NodeCost) + 1;
        var empty = string.Concat(Enumerable.Repeat("<![CDATA[]]>", sections));

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", empty)), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task AFileWithTooManyDistinctNamesIsUnreadable()
    {
        // A name can outlive the run in the cache of LINQ to XML, so the names one file may bring are capped.
        var elements = string.Concat(Enumerable.Range(1, XtfMetadataExtractorProcess.MaxDistinctNames + 1).Select(number => $"<D:n{number}/>"));

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", elements)), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task ANameLongerThanTheLimitIsUnreadable()
    {
        // Names are capped in length as well, or a few long ones would outweigh the cap on their number.
        var atTheLimit = new string('n', XtfMetadataExtractorProcess.MaxNameLength);

        var accepted = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", $"449<D:{atTheLimit}/>")), TestContext.CancellationToken);
        var refused = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", $"449<D:{atTheLimit}n/>")), TestContext.CancellationToken);

        Assert.AreEqual("449", accepted.Scope);
        Assert.AreEqual(XtfFileState.Unreadable, refused.FileState);
    }

    [TestMethod]
    public async Task ANamespaceLongerThanTheLimitIsUnreadable()
    {
        var namespaceUri = "urn:" + new string('n', XtfMetadataExtractorProcess.MaxNameLength - "urn:".Length + 1);

        var result = await Extractor(BfsNumberPath).RunAsync(Transfer(Gemeinde("g1", $"449<x xmlns=\"{namespaceUri}\"/>")), TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Unreadable, result.FileState);
    }

    [TestMethod]
    public async Task ASkippedObjectMayNestAsDeepAsALoadedOne()
    {
        // An empty element right at the depth limit is accepted, whether the object is loaded or only skipped.
        var levels = XtfMetadataExtractorProcess.MaxObjectDepth - 1;
        var atTheLimit = string.Concat(Enumerable.Repeat("<D:x>", levels)) + "<D:y/>" + string.Concat(Enumerable.Repeat("</D:x>", levels));
        var file = Transfer(Gemeinde("g1", "449") + $"<D:Gemeindegrenze ili:tid=\"gg1\">{atTheLimit}</D:Gemeindegrenze>");

        var skipped = await Extractor(BfsNumberPath).RunAsync(file, TestContext.CancellationToken);
        var loaded = await Extractor(BfsNumberPath, XtfMetadataFetch.CurrentlyValid).RunAsync(file, TestContext.CancellationToken);

        Assert.AreEqual(XtfFileState.Readable, skipped.FileState);
        Assert.AreEqual(XtfFileState.Readable, loaded.FileState);
    }

    [TestMethod]
    [DataRow("DMAV_HoheitsgrenzenAV_V1_0:Gemeinde[", DisplayName = "No valid XPath")]
    [DataRow("Gemeinde/BFSNummer", DisplayName = "First step without Model:Class")]
    [DataRow("DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer = '449'", DisplayName = "Computes a value")]
    public async Task AnInvalidPathFailsTheStepNamingThePath(string path)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(path).RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken));

        // A failed step reports the innermost exception, so that one has to name the path.
        Assert.Contains(path, exception.GetBaseException().Message);
    }

    [TestMethod]
    public async Task AnEmptyPathFailsTheStep()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor("  ").RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken));

        Assert.Contains("empty", exception.GetBaseException().Message);
    }

    [TestMethod]
    public async Task AnUnknownFetchValueFailsTheStep()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(BfsNumberPath, (XtfMetadataFetch)7).RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), TestContext.CancellationToken));

        Assert.Contains("<7>", exception.Message);
    }

    [TestMethod]
    public async Task ACancelledRunIsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Extractor(BfsNumberPath).RunAsync(Fixture("XtfMetadata/dmav_municipality.xtf"), cancellation.Token));
    }

    private static XtfMetadataExtractorProcess Extractor(string path, XtfMetadataFetch fetch = XtfMetadataFetch.Single) =>
        new(new XtfMetadataQuery { Path = path, Fetch = fetch }, NullLogger.Instance);

    private static PipelineFile Fixture(string relativePath) => new($"TestData/{relativePath}", Path.GetFileName(relativePath));

    // A minimal transfer with one HoheitsgrenzenAV basket, for cases the derived files do not cover. It binds the DMAV
    // namespace to the prefix D on purpose: a path names models, not the prefixes of a file.
    private PipelineFile Transfer(string objects, string header = "")
    {
        var content = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ili:transfer xmlns:ili="http://www.interlis.ch/xtf/2.4/INTERLIS" xmlns:D="http://www.interlis.ch/xtf/2.4/DMAV_HoheitsgrenzenAV_V1_0">
            <ili:headersection>{header}</ili:headersection>
            <ili:datasection><D:HoheitsgrenzenAV ili:bid="b1">{objects}</D:HoheitsgrenzenAV></ili:datasection>
            </ili:transfer>
            """;
        return WriteFile(content);
    }

    private PipelineFile WriteFile(string content)
    {
        var path = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.xtf");
        File.WriteAllText(path, content);
        return new PipelineFile(path, Path.GetFileName(path));
    }

    private static string Gemeinde(string tid, string bfsNumber, string entstehung = "n1", string? untergang = null)
    {
        var untergangElement = untergang is null ? string.Empty : $"<D:Untergang ili:ref=\"{untergang}\"/>";
        return $"<D:Gemeinde ili:tid=\"{tid}\"><D:BFSNummer>{bfsNumber}</D:BFSNummer><D:Entstehung ili:ref=\"{entstehung}\"/>{untergangElement}</D:Gemeinde>";
    }

    private static string Municipalities(int count) =>
        string.Concat(Enumerable.Range(1, count).Select(number => Gemeinde($"g{number}", number.ToString(CultureInfo.InvariantCulture))));

    private static string Nachfuehrung(string tid, bool approved)
    {
        var approval = approved ? "<D:GenehmigtAm>2021-06-08T12:00:00</D:GenehmigtAm>" : string.Empty;
        return $"<D:HHGNachfuehrung ili:tid=\"{tid}\">{approval}</D:HHGNachfuehrung>";
    }

    private static string Nested(int levels) =>
        string.Concat(Enumerable.Repeat("<D:x>", levels)) + "449" + string.Concat(Enumerable.Repeat("</D:x>", levels));
}
