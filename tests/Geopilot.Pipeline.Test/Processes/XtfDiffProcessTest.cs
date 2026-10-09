using Geopilot.Pipeline.Processes.XtfDiff;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfDiffProcessTest
{
    private const string TwoChanges = """[{"oid":"a","changeType":"added"},{"oid":"b","changeType":"deleted"}]""";

    [TestMethod]
    public async Task ReportsTheChangesFromTheOldToTheNewState()
    {
        var client = new XtfDiffClientFake(compared: true, diffContent: TwoChanges, logContent: "Total changes found: 2");
        var process = CreateProcess(client, modelDirs: null);

        var result = await process.RunAsync(CreateFile("stand_alt.xtf"), CreateFile("stand_neu.xtf"), CancellationToken.None);

        Assert.AreEqual(ComparisonState.Compared, result.ComparisonState);
        Assert.AreEqual("diff.json", result.Diff?.OriginalFileName);
        Assert.AreEqual("diffLog.log", result.Log.OriginalFileName);
        Assert.AreEqual("stand_alt.xtf", client.OldTransferFile?.OriginalFileName, "The old state has to reach the tool as the old one.");
        Assert.AreEqual("stand_neu.xtf", client.NewTransferFile?.OriginalFileName, "The new state has to reach the tool as the new one.");

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "2 Änderungen gefunden." },
            { "fr", "2 modifications trouvées." },
            { "it", "2 modifiche trovate." },
            { "en", "2 changes found." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    [DataRow("[]", "Keine Änderungen gefunden.")]
    [DataRow("""[{"oid":"a","changeType":"changed"}]""", "Eine Änderung gefunden.")]
    public async Task NamesTheNumberOfChanges(string diffContent, string expectedMessage)
    {
        var process = CreateProcess(new XtfDiffClientFake(compared: true, diffContent, logContent: string.Empty), modelDirs: null);

        var result = await process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None);

        Assert.AreEqual(expectedMessage, result.StatusMessage["de"]);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
    }

    [TestMethod]
    public async Task ReportsStatesOfDifferentInterlisVersionsAsNotComparable()
    {
        var log = $"ERROR ch.geowerkstatt.xtfdifftool.Main - Error processing XTF files\njava.lang.IllegalStateException: {XtfDiffProcess.DifferentInterlisVersionsMarker}\n";
        var process = CreateProcess(new XtfDiffClientFake(compared: false, diffContent: null, log), modelDirs: null);

        var result = await process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None);

        // Not comparable is a result and not an error: the definition decides whether it blocks the delivery.
        Assert.AreEqual(ComparisonState.NotComparable, result.ComparisonState);
        Assert.IsNull(result.Diff, "There is no diff to offer when the tool could not compare the states.");
        Assert.AreEqual("diffLog.log", result.Log.OriginalFileName);
        Assert.Contains("INTERLIS-Versionen", result.StatusMessage["de"]);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
    }

    [TestMethod]
    public async Task ReportsStatesOfDifferentModelsAsNotComparable()
    {
        var log = $"ERROR ch.geowerkstatt.xtfdifftool.Main - Error processing XTF files\njava.lang.IllegalStateException: {XtfDiffProcess.DifferentModelsMarker}\n";
        var process = CreateProcess(new XtfDiffClientFake(compared: false, diffContent: null, log), modelDirs: null);

        var result = await process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None);

        Assert.AreEqual(ComparisonState.NotComparable, result.ComparisonState);
        Assert.IsNull(result.Diff);
        Assert.Contains("Modelle", result.StatusMessage["de"]);
    }

    [TestMethod]
    public async Task ThrowsWhenTheToolFailsForAnotherReason()
    {
        // A failure the log does not explain is not a verdict about the two states, so the step fails.
        var log = "ERROR ch.geowerkstatt.xtfdifftool.Main - Error processing XTF files\njava.lang.IllegalStateException: Duplicate TID encountered x\n";
        var process = CreateProcess(new XtfDiffClientFake(compared: false, diffContent: null, log), modelDirs: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None));

        Assert.Contains("old.xtf", exception.Message);
        Assert.Contains("new.xtf", exception.Message);
    }

    [TestMethod]
    public async Task PassesTheConfiguredModelRepositoriesTrimmed()
    {
        var client = new XtfDiffClientFake(compared: true, diffContent: "[]", logContent: string.Empty);
        var process = CreateProcess(client, modelDirs: " %REPOSITORIES/dmav@0.1.1 ; https://models.interlis.ch/ ");

        await process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "%REPOSITORIES/dmav@0.1.1", "https://models.interlis.ch/" }, client.ModelDirs?.ToArray());
    }

    [TestMethod]
    public async Task PassesNoModelRepositoriesWhenNoneAreConfigured()
    {
        var client = new XtfDiffClientFake(compared: true, diffContent: "[]", logContent: string.Empty);
        var process = CreateProcess(client, modelDirs: " ");

        await process.RunAsync(CreateFile("old.xtf"), CreateFile("new.xtf"), CancellationToken.None);

        // Without an entry the wrapper falls back to the default of the tool.
        Assert.IsNull(client.ModelDirs);
    }

    private static XtfDiffProcess CreateProcess(XtfDiffClientFake client, string? modelDirs)
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "XtfDiffProcess");
        return new XtfDiffProcess(modelDirs, client, pipelineFileManager, Mock.Of<ILogger<XtfDiffProcessTest>>());
    }

    private static PipelineFile CreateFile(string originalFileName)
    {
        return new PipelineFile(Path.Combine("TestData", "Ilitools", "transfer.xtf"), originalFileName);
    }
}
