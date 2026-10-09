using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Processes;

/// <summary>
/// Stands in for the XTF-Diff-Tool behind the ilitools-wrapper: writes the given log and diff into the files the process
/// hands over, the way the real client does with what the tool returns, and records what it was called with.
/// </summary>
internal sealed class XtfDiffClientFake : IXtfDiffClient
{
    private readonly bool compared;
    private readonly string? diffContent;
    private readonly string logContent;

    public XtfDiffClientFake(bool compared, string? diffContent, string logContent)
    {
        this.compared = compared;
        this.diffContent = diffContent;
        this.logContent = logContent;
    }

    public IReadOnlyList<string>? ModelDirs { get; private set; }

    public IPipelineFile? OldTransferFile { get; private set; }

    public IPipelineFile? NewTransferFile { get; private set; }

    public async Task<bool> DiffAsync(
        IReadOnlyList<string>? modelDirs,
        IPipelineFile oldTransferFile,
        IPipelineFile newTransferFile,
        IPipelineFile logFile,
        IPipelineFile diffFile,
        CancellationToken cancellationToken)
    {
        ModelDirs = modelDirs;
        OldTransferFile = oldTransferFile;
        NewTransferFile = newTransferFile;

        await WriteAsync(logFile, logContent, cancellationToken);
        if (diffContent is not null)
            await WriteAsync(diffFile, diffContent, cancellationToken);

        return compared;
    }

    private static async Task WriteAsync(IPipelineFile file, string content, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenWriteFileStream();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }
}
