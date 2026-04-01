using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Api.Pipeline.Process.Matcher.XtfMatcher;

internal class XtfMatcherProcess
{
    private readonly HashSet<string> fileExtensions;
    private readonly HashSet<string> iliModels;
    private readonly HashSet<string> fileNamePatterns;

    public XtfMatcherProcess(string? fileExtensions, string? iliModels, string? fileNamePatterns)
    {
        this.fileExtensions = ParseCommaSeparatedConfiguration(fileExtensions);
        this.iliModels = ParseCommaSeparatedConfiguration(iliModels);
        this.fileNamePatterns = ParseCommaSeparatedConfiguration(fileNamePatterns);
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync([UploadFiles] IPipelineFileList uploadFiles)
    {
        return new Dictionary<string, object?>()
        {
            { "xtf_files", uploadFiles.Files.FirstOrDefault() },
        };
    }

    private static HashSet<string> ParseCommaSeparatedConfiguration(string? commaSeparatedValues)
    {
        if (string.IsNullOrEmpty(commaSeparatedValues))
            return new HashSet<string>();
        else
            return commaSeparatedValues.Split(',').ToHashSet();
    }
}
