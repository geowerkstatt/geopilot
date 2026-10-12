using Geopilot.Pipeline.Processes.Matcher.OldNewMatcher;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class OldNewMatcherProcessTest
{
    [TestMethod]
    public async Task RoleWordsAssignTheStates()
    {
        // The new state comes first: the role words decide, not the order of the upload.
        var result = await CreateProcess().RunAsync([CreateFile("stand_neu.xtf"), CreateFile("stand_alt.xtf")]);

        AssertNames(["stand_alt.xtf"], result.OldFiles);
        AssertNames(["stand_neu.xtf"], result.NewFiles);
        AssertNames([], result.UnmatchedFiles);
        Assert.AreEqual(RoleDecision.RoleWords, result.DecidedBy);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Alter Stand: stand_alt.xtf, neuer Stand: stand_neu.xtf." },
            { "fr", "Ancien état : stand_alt.xtf, nouvel état : stand_neu.xtf." },
            { "it", "Stato precedente: stand_alt.xtf, stato attuale: stand_neu.xtf." },
            { "en", "Old state: stand_alt.xtf, new state: stand_neu.xtf." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    [DataRow("plan_alt.xtf", "plan.xtf", "plan_alt.xtf", "plan.xtf")]
    [DataRow("plan.xtf", "plan_alt.xtf", "plan_alt.xtf", "plan.xtf")]
    [DataRow("plan_new.xtf", "plan.xtf", "plan.xtf", "plan_new.xtf")]
    [DataRow("plan.xtf", "plan_new.xtf", "plan.xtf", "plan_new.xtf")]
    public async Task OneRoleWordDecidesForBothOfTwoFiles(string first, string second, string expectedOld, string expectedNew)
    {
        var result = await CreateProcess().RunAsync([CreateFile(first), CreateFile(second)]);

        AssertNames([expectedOld], result.OldFiles);
        AssertNames([expectedNew], result.NewFiles);
        Assert.AreEqual(RoleDecision.RoleWords, result.DecidedBy);
    }

    [TestMethod]
    [DataRow("stand-alt.xtf")]
    [DataRow("stand alt.xtf")]
    [DataRow("stand.alt.xtf")]
    [DataRow("STAND_ALT.xtf")]
    public async Task RecognizesRoleWordsAtAnySeparatorAndInAnyCase(string oldName)
    {
        var result = await CreateProcess().RunAsync([CreateFile("stand.xtf"), CreateFile(oldName)]);

        AssertNames([oldName], result.OldFiles);
        Assert.AreEqual(RoleDecision.RoleWords, result.DecidedBy);
    }

    [TestMethod]
    public async Task NameOrderDecidesForTwoFilesWithoutRoleWords()
    {
        var result = await CreateProcess().RunAsync([CreateFile("gemeinde_2026-10-01.xtf"), CreateFile("gemeinde_2025-01-01.xtf")]);

        AssertNames(["gemeinde_2025-01-01.xtf"], result.OldFiles);
        AssertNames(["gemeinde_2026-10-01.xtf"], result.NewFiles);
        Assert.AreEqual(RoleDecision.NameOrder, result.DecidedBy);
        Assert.AreEqual(
            "Kein Dateiname sagt, welcher Stand alt und welcher neu ist, darum nach Namen sortiert. Alter Stand: gemeinde_2025-01-01.xtf, neuer Stand: gemeinde_2026-10-01.xtf.",
            result.StatusMessage["de"]);
        Assert.HasCount(4, result.StatusMessage.Languages, "The status message has to stay available in every supported language.");
    }

    [TestMethod]
    [DataRow("B.xtf", "a.xtf", "a.xtf", DisplayName = "The case does not decide")]
    [DataRow("Neuchatel.xtf", "Altlasten.xtf", "Altlasten.xtf", DisplayName = "A role word inside another word does not count")]
    public async Task NameOrderSortsAscendingIgnoringTheCase(string first, string second, string expectedOld)
    {
        var result = await CreateProcess().RunAsync([CreateFile(first), CreateFile(second)]);

        AssertNames([expectedOld], result.OldFiles);
        Assert.AreEqual(RoleDecision.NameOrder, result.DecidedBy);
    }

    [TestMethod]
    public async Task WithoutConfiguredRoleWordsTheNamesDecide()
    {
        var process = new OldNewMatcherProcess(oldWords: null, newWords: null);

        var result = await process.RunAsync([CreateFile("stand_neu.xtf"), CreateFile("stand_alt.xtf")]);

        AssertNames(["stand_alt.xtf"], result.OldFiles);
        Assert.AreEqual(RoleDecision.NameOrder, result.DecidedBy);
    }

    [TestMethod]
    public async Task ContradictingRoleWordsLeaveOneStateWithoutAFile()
    {
        // Two files for the new state: no fallback, the definition decides with a condition on the counts.
        var result = await CreateProcess().RunAsync([CreateFile("stand_neu_2.xtf"), CreateFile("stand_neu_1.xtf")]);

        AssertNames([], result.OldFiles);
        AssertNames(["stand_neu_2.xtf", "stand_neu_1.xtf"], result.NewFiles);
        Assert.AreEqual(RoleDecision.RoleWords, result.DecidedBy);

        LocalizedText expected = new Dictionary<string, string>
        {
            { "de", "Alter Stand: keine Datei, neuer Stand: stand_neu_2.xtf, stand_neu_1.xtf." },
            { "fr", "Ancien état : aucun fichier, nouvel état : stand_neu_2.xtf, stand_neu_1.xtf." },
            { "it", "Stato precedente: nessun file, stato attuale: stand_neu_2.xtf, stand_neu_1.xtf." },
            { "en", "Old state: no file, new state: stand_neu_2.xtf, stand_neu_1.xtf." },
        };
        Assert.AreEqual(expected, result.StatusMessage);
    }

    [TestMethod]
    public async Task FileWithWordsOfBothRolesStaysUnmatched()
    {
        var result = await CreateProcess().RunAsync([CreateFile("alt_neu.xtf"), CreateFile("plan.xtf")]);

        AssertNames([], result.OldFiles);
        AssertNames([], result.NewFiles);
        AssertNames(["alt_neu.xtf", "plan.xtf"], result.UnmatchedFiles);
    }

    [TestMethod]
    public async Task MoreThanTwoFilesAreMatchedByRoleWordsAlone()
    {
        var result = await CreateProcess().RunAsync([CreateFile("plan.xtf"), CreateFile("plan_neu.xtf"), CreateFile("plan_alt.xtf")]);

        AssertNames(["plan_alt.xtf"], result.OldFiles);
        AssertNames(["plan_neu.xtf"], result.NewFiles);
        AssertNames(["plan.xtf"], result.UnmatchedFiles);
        Assert.AreEqual(RoleDecision.RoleWords, result.DecidedBy);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public async Task OnlyTwoFilesFallBackToTheNameOrder(int fileCount)
    {
        var files = Enumerable.Range(1, fileCount).Select(number => CreateFile($"gemeinde_{number}.xtf")).ToArray();

        var result = await CreateProcess().RunAsync(files);

        AssertNames([], result.OldFiles);
        AssertNames([], result.NewFiles);
        Assert.HasCount(fileCount, result.UnmatchedFiles);
    }

    private static OldNewMatcherProcess CreateProcess()
    {
        return new OldNewMatcherProcess(oldWords: ["alt", "old"], newWords: ["neu", "new"]);
    }

    private static PipelineFile CreateFile(string originalFileName)
    {
        return new PipelineFile(Path.Combine("TestData", "Ilitools", "transfer.xtf"), originalFileName);
    }

    private static void AssertNames(string[] expected, IPipelineFile[] actual)
    {
        CollectionAssert.AreEqual(expected, actual.Select(file => file.OriginalFileName).ToArray());
    }
}
