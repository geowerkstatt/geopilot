namespace Geopilot.Api.Test.Pipeline.Process;

/// <summary>
/// Plays the role of the Hop worker against a shared jobs directory: waits for the client's
/// <c>input.ready</c> sentinel, captures what was dropped, writes the requested output files and log,
/// then signals <c>output.ready</c> last. Used to drive <see cref="Geopilot.Api.Pipeline.Process.Hop.HopClient"/>
/// from tests without a real worker container.
/// </summary>
internal static class HopWorkerSimulator
{
    /// <summary>
    /// Waits for a single job and completes it.
    /// </summary>
    /// <param name="jobsDirectory">The shared jobs directory the client writes into.</param>
    /// <param name="success"><c>true</c> writes <c>success.log</c>, <c>false</c> writes <c>error.log</c>, <c>null</c> writes neither (to exercise the "no log" branch).</param>
    /// <param name="outputFiles">Files to drop into <c>output/</c>, keyed by forward-slash relative path.</param>
    /// <param name="log">Content for the produced log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the worker observed the client write.</returns>
    public static Task<HopJobObservation> RunAsync(
        string jobsDirectory,
        bool? success,
        IReadOnlyDictionary<string, string> outputFiles,
        string log,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            async () =>
            {
                var jobDirectory = await WaitForJobAsync(jobsDirectory, cancellationToken);

                var argsJson = await File.ReadAllTextAsync(Path.Combine(jobDirectory, "args.json"), cancellationToken);
                var inputFiles = await ReadInputTreeAsync(Path.Combine(jobDirectory, "input"), cancellationToken);

                var outputDirectory = Path.Combine(jobDirectory, "output");
                Directory.CreateDirectory(outputDirectory);
                foreach (var (relativePath, content) in outputFiles)
                {
                    var fullPath = Path.Combine(outputDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    await File.WriteAllTextAsync(fullPath, content, cancellationToken);
                }

                if (success == true)
                {
                    await File.WriteAllTextAsync(Path.Combine(jobDirectory, "success.log"), log, cancellationToken);
                }
                else if (success == false)
                {
                    await File.WriteAllTextAsync(Path.Combine(jobDirectory, "error.log"), log, cancellationToken);
                }

                // output.ready is written last, mirroring the worker contract.
                await File.WriteAllTextAsync(Path.Combine(jobDirectory, "output.ready"), string.Empty, cancellationToken);

                return new HopJobObservation(argsJson, inputFiles);
            },
            cancellationToken);
    }

    private static async Task<string> WaitForJobAsync(string jobsDirectory, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(jobsDirectory))
            {
                var jobDirectory = Directory.EnumerateDirectories(jobsDirectory)
                    .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "input.ready")));
                if (jobDirectory is not null)
                {
                    return jobDirectory;
                }
            }

            await Task.Delay(20, cancellationToken);
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadInputTreeAsync(string inputDirectory, CancellationToken cancellationToken)
    {
        var inputFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(inputDirectory))
        {
            return inputFiles;
        }

        foreach (var path in Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(inputDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
            inputFiles[relativePath] = await File.ReadAllTextAsync(path, cancellationToken);
        }

        return inputFiles;
    }
}
