using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Globalization;

namespace Geopilot.Pipeline.Processes.Matcher.OldNewMatcher;

/// <summary>
/// Matches files to an old and a new state, so a process that compares states takes each from a parameter of its own
/// (ADR 0019). Role words in the file names decide; only two files without any role word fall back to the ascending
/// order of their names. How many files a state may have is up to the pipeline, which checks the counts with a condition.
/// </summary>
internal class OldNewMatcherProcess
{
    private static readonly char[] WordSeparators = ['_', '-', '.', ' '];

    private static readonly LocalizedText RoleWordsStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Alter Stand: {0}, neuer Stand: {1}." },
        { "fr", "Ancien état : {0}, nouvel état : {1}." },
        { "it", "Stato precedente: {0}, stato attuale: {1}." },
        { "en", "Old state: {0}, new state: {1}." },
    };

    private static readonly LocalizedText NameOrderStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Kein Dateiname sagt, welcher Stand alt und welcher neu ist, darum nach Namen sortiert. Alter Stand: {0}, neuer Stand: {1}." },
        { "fr", "Aucun nom de fichier n'indique quel état est l'ancien et lequel est le nouveau, ils sont donc triés par nom. Ancien état : {0}, nouvel état : {1}." },
        { "it", "Nessun nome di file indica quale stato è il precedente e quale l'attuale, quindi sono ordinati per nome. Stato precedente: {0}, stato attuale: {1}." },
        { "en", "No file name tells which state is old and which is new, so they are sorted by name. Old state: {0}, new state: {1}." },
    };

    private static readonly LocalizedText NoFileText = new Dictionary<string, string>
    {
        { "de", "keine Datei" },
        { "fr", "aucun fichier" },
        { "it", "nessun file" },
        { "en", "no file" },
    };

    private readonly HashSet<string> oldWords;
    private readonly HashSet<string> newWords;

    /// <summary>
    /// Initializes a new instance of the <see cref="OldNewMatcherProcess"/> class with the role words of the pipeline.
    /// </summary>
    /// <param name="oldWords">Words that mark the old state in a file name, for example "alt" and "old". Case-insensitive, whole words only.</param>
    /// <param name="newWords">Words that mark the new state in a file name, for example "neu" and "new". Case-insensitive, whole words only.</param>
    public OldNewMatcherProcess(HashSet<string>? oldWords, HashSet<string>? newWords)
    {
        this.oldWords = new HashSet<string>(oldWords ?? [], StringComparer.OrdinalIgnoreCase);
        this.newWords = new HashSet<string>(newWords ?? [], StringComparer.OrdinalIgnoreCase);
    }

    [PipelineProcessRun]
    public Task<OldNewMatcherResult> RunAsync(IPipelineFile[] files)
    {
        var (roles, decidedBy) = AssignRoles(files);
        var oldFiles = files.Where((_, index) => roles[index] == Role.Old).ToArray();
        var newFiles = files.Where((_, index) => roles[index] == Role.New).ToArray();
        var statusMessageFormat = decidedBy == RoleDecision.NameOrder ? NameOrderStatusMessageFormat : RoleWordsStatusMessageFormat;

        return Task.FromResult(new OldNewMatcherResult
        {
            OldFiles = oldFiles,
            NewFiles = newFiles,
            UnmatchedFiles = files.Except(oldFiles).Except(newFiles).ToArray(),
            DecidedBy = decidedBy,
            StatusMessage = FormatStatusMessage(statusMessageFormat, oldFiles, newFiles),
        });
    }

    /// <summary>
    /// Each file takes the role its words name. Of exactly two files, one with a role word decides for the other without
    /// any, and two without any are taken in name order. Contradicting role words never fall back to the names.
    /// </summary>
    private (Role[] Roles, RoleDecision DecidedBy) AssignRoles(IPipelineFile[] files)
    {
        var roles = files.Select(RoleOf).ToArray();
        if (files.Length != 2)
            return (roles, RoleDecision.RoleWords);

        return (roles[0], roles[1]) switch
        {
            (Role.Old, Role.None) or (Role.None, Role.New) => ([Role.Old, Role.New], RoleDecision.RoleWords),
            (Role.New, Role.None) or (Role.None, Role.Old) => ([Role.New, Role.Old], RoleDecision.RoleWords),
            (Role.None, Role.None) => (RolesByNameOrder(files[0], files[1]), RoleDecision.NameOrder),
            _ => (roles, RoleDecision.RoleWords),
        };
    }

    private Role RoleOf(IPipelineFile file)
    {
        var words = file.OriginalFileName.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        return (words.Any(oldWords.Contains), words.Any(newWords.Contains)) switch
        {
            (true, true) => Role.Both,
            (true, false) => Role.Old,
            (false, true) => Role.New,
            (false, false) => Role.None,
        };
    }

    /// <summary>
    /// Ascending, so that a date in the names (2025 before 2026) points the same way as the role words. Ignoring the case
    /// first, then by ordinal, so the same two names always match the same way.
    /// </summary>
    private static Role[] RolesByNameOrder(IPipelineFile first, IPipelineFile second)
    {
        var comparison = StringComparer.OrdinalIgnoreCase.Compare(first.OriginalFileName, second.OriginalFileName);
        if (comparison == 0)
            comparison = StringComparer.Ordinal.Compare(first.OriginalFileName, second.OriginalFileName);

        return comparison <= 0 ? [Role.Old, Role.New] : [Role.New, Role.Old];
    }

    /// <summary>
    /// Fills the names of the old and the new files into each language of the format, "no file" for a state without one.
    /// </summary>
    private static LocalizedText FormatStatusMessage(LocalizedText format, IPipelineFile[] oldFiles, IPipelineFile[] newFiles)
    {
        return format.ToDictionary().ToDictionary(
            message => message.Key,
            message => string.Format(CultureInfo.InvariantCulture, message.Value, JoinNames(oldFiles, message.Key), JoinNames(newFiles, message.Key)));
    }

    private static string JoinNames(IPipelineFile[] files, string language)
    {
        return files.Length == 0
            ? NoFileText[language] ?? string.Empty
            : string.Join(", ", files.Select(file => file.OriginalFileName));
    }

    private enum Role
    {
        None,
        Old,
        New,
        Both,
    }
}
