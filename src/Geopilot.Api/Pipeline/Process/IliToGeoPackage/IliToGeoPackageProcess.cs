using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline.Process.Container;

namespace Geopilot.Api.Pipeline.Process.IliToGeoPackage;

/// <summary>
/// Pipeline process that converts INTERLIS transfer files (XTF/ITF) to GeoPackage (.gpkg) by running the
/// <c>ili2gpkg</c> CLI tool inside a one-shot Docker container.
/// </summary>
/// <remarks>
/// The container image is hardcoded as <see cref="Image"/>. The pattern must be added to
/// <c>Docker:AllowedImages</c> in configuration for this process to run. Each input file is processed by
/// a separate container invocation; input is bind-mounted at <c>/data/in</c>, output at <c>/data/out</c>.
/// Produced files are exposed as the step output <c>gpkg_files</c>.
/// </remarks>
internal class IliToGeoPackageProcess
{
    /// <summary>
    /// Container image used to run ili2gpkg. This must be covered by the <c>Docker:AllowedImages</c> configuration.
    /// </summary>
    public const string Image = "ghcr.io/geowerkstatt/ili2gpkg:5.1.0";

    private const string ContainerInputDirectory = "/data/in";
    private const string ContainerOutputDirectory = "/data/out";
    private const string ContainerModelDirectory = "/data/models";
    private const string ContainerIliCacheDirectory = "/root/.ilicache";

    private readonly bool import;
    private readonly bool disableValidation;
    private readonly bool createBasketCol;
    private readonly bool smart2Inheritance;
    private readonly bool schemaImport;
    private readonly HashSet<string> models;
    private readonly string? modelDir;
    private readonly int? defaultSrsCode;
    private readonly string? iliCacheDir;
    private readonly IContainerRunner containerRunner;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IliToGeoPackageProcess"/> class.
    /// </summary>
    /// <param name="import">If true, passes <c>--import</c> to ili2gpkg to import data from the XTF into the GeoPackage. Defaults to true. Set to false when only <see cref="schemaImport"/> is desired (schema-only run, no data).</param>
    /// <param name="disableValidation">If true, passes <c>--disableValidation</c> to ili2gpkg to skip validation during import (useful when upstream steps already validated the data). Defaults to false (validation runs).</param>
    /// <param name="createBasketCol">If true, passes <c>--createBasketCol</c> to add a <c>T_basket</c> column on each table, tagging rows with the dataset they came from. Required to use dataset-based semantics. Defaults to false.</param>
    /// <param name="smart2Inheritance">If true, passes <c>--smart2Inheritance</c> to use the "smart2" INTERLIS inheritance mapping strategy (one table per concrete class, with discriminator). Defaults to false (standard mapping).</param>
    /// <param name="schemaImport">If true, passes <c>--schemaimport</c> to create the GeoPackage schema from the INTERLIS model before importing data. Defaults to false (import expects the schema to already exist or to be auto-created from the XTF model).</param>
    /// <param name="models">Set of INTERLIS model names, passed via <c>--models</c> as a semicolon-separated list (ili2gpkg's expected syntax). Null or empty lets ili2gpkg infer the models from the XTF header.</param>
    /// <param name="modelDir">Optional host path to a local INTERLIS model directory. Bind-mounted read-only at <c>/data/models</c> and passed via <c>--modeldir</c>.</param>
    /// <param name="defaultSrsCode">Optional EPSG code of the default coordinate reference system, passed via <c>--defaultSrsCode</c> (e.g. <c>2056</c> for Swiss CH1903+/LV95). Null lets ili2gpkg use its default.</param>
    /// <param name="iliCacheDir">Optional host path used as persistent INTERLIS model cache. Bind-mounted at <c>/root/.ilicache</c> inside the container. Reusing a cache across runs avoids re-downloading models from <c>models.interlis.ch</c>, saving noticeable time per invocation. The directory is created on first use if it does not exist.</param>
    /// <param name="containerRunner">Container runner service.</param>
    /// <param name="pipelineFileManager">Used to register produced .gpkg files into the pipeline working directory.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public IliToGeoPackageProcess(
        bool? import,
        bool? disableValidation,
        bool? createBasketCol,
        bool? smart2Inheritance,
        bool? schemaImport,
        HashSet<string>? models,
        string? modelDir,
        int? defaultSrsCode,
        string? iliCacheDir,
        IContainerRunner containerRunner,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.import = import ?? true;
        this.disableValidation = disableValidation ?? false;
        this.createBasketCol = createBasketCol ?? false;
        this.smart2Inheritance = smart2Inheritance ?? false;
        this.schemaImport = schemaImport ?? false;
        this.models = models ?? new HashSet<string>();
        this.modelDir = modelDir;
        this.defaultSrsCode = defaultSrsCode;
        this.iliCacheDir = iliCacheDir;
        this.containerRunner = containerRunner ?? throw new ArgumentNullException(nameof(containerRunner));
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;

        // Ensure the cache directory exists on the host so the container's bind mount has
        // a source to map. Docker would otherwise either fail or create an unwritable root-owned
        // directory, depending on platform.
        if (!string.IsNullOrEmpty(iliCacheDir))
            Directory.CreateDirectory(iliCacheDir);
    }

    /// <summary>
    /// Runs ili2gpkg once per input file and returns the produced GeoPackage files.
    /// </summary>
    /// <param name="iliFiles">INTERLIS transfer files (XTF/ITF) piped in from an upstream step (e.g. a matcher or validator).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary with <c>gpkg_files</c> as an <see cref="IPipelineFile"/> array.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile[] iliFiles, CancellationToken cancellationToken)
    {
        var gpkgFiles = new List<IPipelineFile>();
        var warningsCount = 0;

        // A single shared work directory for the whole step — one "in" and one "out" bind-mounted
        // directory is reused across all input files. This keeps filesystem churn low and keeps
        // all step artifacts co-located under the pipeline working directory.
        var workDir = PrepareWorkDirectory();

        foreach (var inputFile in iliFiles)
        {
            var result = await ConvertFileAsync(inputFile, workDir, cancellationToken);
            gpkgFiles.Add(result.GpkgFile);
            if (result.HadWarnings)
                warningsCount++;
        }

        return new Dictionary<string, object?>
        {
            { "gpkg_files", gpkgFiles.ToArray() },
            { "status_message", BuildStatusMessage(gpkgFiles.Count, warningsCount) },
        };
    }

    /// <summary>
    /// Converts a single INTERLIS transfer file to a GeoPackage by running ili2gpkg in a one-shot container.
    /// </summary>
    /// <param name="inputFile">The XTF/ITF file to convert.</param>
    /// <param name="workDir">Shared bind-mount directory pair for the step (see <see cref="PrepareWorkDirectory"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ConversionResult"/> with the produced GeoPackage and whether ili2gpkg reported warnings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ili2gpkg does not produce the expected output file.</exception>
    internal async Task<ConversionResult> ConvertFileAsync(IPipelineFile inputFile, WorkDirectory workDir, CancellationToken cancellationToken)
    {
        await CopyInputFileAsync(inputFile, workDir, cancellationToken);
        var spec = BuildContainerSpec(inputFile, workDir);

        logger.LogInformation("Running ili2gpkg '{Image}' for '{File}': {Args}", Image, inputFile.OriginalFileName, string.Join(' ', spec.Command));
        var runResult = await containerRunner.RunAsync(spec, cancellationToken);
        logger.LogInformation("completed ili2gpkg for '{File}' with exit code {ExitCode} in {ElapsedTime}", inputFile.OriginalFileName, runResult.ExitCode, runResult.Duration);

        // The presence of the expected output file is the authoritative success signal:
        // ili2gpkg returns non-zero exit codes even on successful imports that had warnings
        // (e.g. "no CRS for attribute ..."), so exit code alone is not reliable.
        var producedFile = new FileInfo(Path.Combine(workDir.HostOutputDir, OutputGpkgName(inputFile)));
        if (!producedFile.Exists)
            throw BuildOutputMissingException(inputFile, spec, runResult);

        var gpkgFile = await CopyToPipelineFileAsync(producedFile.FullName, inputFile.OriginalFileNameWithoutExtension, cancellationToken);

        var hadWarnings = runResult.ExitCode != 0;
        if (hadWarnings)
        {
            logger.LogWarning(
                "ili2gpkg completed with non-zero exit code {ExitCode} for file '{File}' but the expected output was produced. Treating as success. Stderr tail: {Stderr}",
                runResult.ExitCode,
                inputFile.OriginalFileName,
                runResult.Stderr);
        }

        return new ConversionResult(gpkgFile, hadWarnings);
    }

    /// <summary>
    /// Creates the <c>in/</c> and <c>out/</c> bind-mount subdirectories inside the pipeline
    /// working directory. A single pair is shared across all input files of the step.
    /// Using the pipeline-owned working dir (not system temp) keeps artifacts discoverable
    /// for debugging and respects the pipeline's cleanup policy.
    /// </summary>
    internal WorkDirectory PrepareWorkDirectory()
    {
        var hostInputDir = Directory.CreateDirectory(Path.Combine(pipelineFileManager.WorkingDirectory, "in"));
        var hostOutputDir = Directory.CreateDirectory(Path.Combine(pipelineFileManager.WorkingDirectory, "out"));
        return new WorkDirectory(hostInputDir.FullName, hostOutputDir.FullName);
    }

    /// <summary>
    /// Copies the given input file into the shared bind-mount input directory so the container can read it.
    /// </summary>
    private static async Task CopyInputFileAsync(IPipelineFile inputFile, WorkDirectory workDir, CancellationToken cancellationToken)
    {
        var hostInputPath = Path.Combine(workDir.HostInputDir, inputFile.OriginalFileName);
        using var src = inputFile.OpenReadFileStream();
        using var dst = File.Create(hostInputPath);
        await src.CopyToAsync(dst, cancellationToken);
    }

    /// <summary>
    /// Builds the container spec (image, command, bind mounts) for converting a single file.
    /// </summary>
    private ContainerRunSpec BuildContainerSpec(IPipelineFile inputFile, WorkDirectory workDir)
    {
        var containerInputPath = $"{ContainerInputDirectory}/{inputFile.OriginalFileName}";
        var containerOutputPath = $"{ContainerOutputDirectory}/{OutputGpkgName(inputFile)}";

        var command = BuildIli2GpkgCommand(containerInputPath, containerOutputPath);

        var mounts = new List<(string HostPath, string ContainerPath)>
        {
            (workDir.HostInputDir, ContainerInputDirectory),
            (workDir.HostOutputDir, ContainerOutputDirectory),
        };
        if (!string.IsNullOrEmpty(modelDir))
            mounts.Add((modelDir, ContainerModelDirectory));
        if (!string.IsNullOrEmpty(iliCacheDir))
            mounts.Add((iliCacheDir, ContainerIliCacheDirectory));

        return new ContainerRunSpec(Image, command, mounts);
    }

    /// <summary>
    /// Copies a file from the host filesystem into a newly generated pipeline file.
    /// </summary>
    /// <param name="sourcePath">Absolute host path of the file to copy from.</param>
    /// <param name="originalBaseName">Original base name (without extension) for the generated pipeline file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated pipeline file containing the copied contents.</returns>
    private async Task<IPipelineFile> CopyToPipelineFileAsync(string sourcePath, string originalBaseName, CancellationToken cancellationToken)
    {
        var pipelineFile = pipelineFileManager.GeneratePipelineFile(originalBaseName, "gpkg");
        using var fromStream = File.OpenRead(sourcePath);
        using var toStream = pipelineFile.OpenWriteFileStream();
        await fromStream.CopyToAsync(toStream, cancellationToken);
        return pipelineFile;
    }

    /// <summary>
    /// Builds the exception describing a missing ili2gpkg output, with the full invocation context
    /// (command, exit code, stdout, stderr) for diagnosability.
    /// </summary>
    private static InvalidOperationException BuildOutputMissingException(IPipelineFile inputFile, ContainerRunSpec spec, ContainerRunResult runResult)
    {
        var outputGpkgName = OutputGpkgName(inputFile);
        return new InvalidOperationException(
            $"ili2gpkg did not produce the expected output file '{outputGpkgName}' for input '{inputFile.OriginalFileName}'." +
            $"{Environment.NewLine}Exit code: {runResult.ExitCode}" +
            $"{Environment.NewLine}Command: {string.Join(' ', spec.Command)}" +
            $"{Environment.NewLine}Stdout: {runResult.Stdout}" +
            $"{Environment.NewLine}Stderr: {runResult.Stderr}");
    }

    private static string OutputGpkgName(IPipelineFile inputFile) =>
        $"{inputFile.OriginalFileNameWithoutExtension}.gpkg";

    /// <summary>
    /// Outcome of converting a single INTERLIS transfer file to a GeoPackage.
    /// </summary>
    /// <param name="GpkgFile">The produced GeoPackage file, registered with the pipeline file manager.</param>
    /// <param name="HadWarnings">True if ili2gpkg completed with warnings (non-zero exit code) but still produced the expected output.</param>
    internal sealed record ConversionResult(IPipelineFile GpkgFile, bool HadWarnings);

    /// <summary>
    /// Host-side paths of the bind-mount input and output directories used by the step.
    /// A single pair is shared across all input files processed by one <see cref="RunAsync"/> call.
    /// </summary>
    /// <param name="HostInputDir">Absolute host path bind-mounted into the container as <c>/data/in</c>.</param>
    /// <param name="HostOutputDir">Absolute host path bind-mounted into the container as <c>/data/out</c>.</param>
    internal sealed record WorkDirectory(string HostInputDir, string HostOutputDir);

    /// <summary>
    /// Builds the per-language status message. Three cases:
    /// <list type="bullet">
    /// <item>No warnings: "N GeoPackages created".</item>
    /// <item>All files had warnings: "N GeoPackages created with warnings".</item>
    /// <item>Some files had warnings: "N GeoPackages created, M with warnings".</item>
    /// </list>
    /// </summary>
    private static Dictionary<string, string> BuildStatusMessage(int count, int warningsCount)
    {
        var isPlural = count != 1;
        var noun = new Dictionary<string, string>
        {
            { "de", isPlural ? "GeoPackages" : "GeoPackage" },
            { "en", isPlural ? "GeoPackages" : "GeoPackage" },
            { "fr", isPlural ? "GeoPackages créés" : "GeoPackage créé" },
            { "it", isPlural ? "GeoPackages creati" : "GeoPackage creato" },
        };

        if (warningsCount == 0)
        {
            return new Dictionary<string, string>
            {
                { "de", $"{count} {noun["de"]} erstellt" },
                { "en", $"{count} {noun["en"]} created" },
                { "fr", $"{count} {noun["fr"]}" },
                { "it", $"{count} {noun["it"]}" },
            };
        }

        if (warningsCount == count)
        {
            return new Dictionary<string, string>
            {
                { "de", $"{count} {noun["de"]} mit Warnungen erstellt" },
                { "en", $"{count} {noun["en"]} created with warnings" },
                { "fr", $"{count} {noun["fr"]} avec des avertissements" },
                { "it", $"{count} {noun["it"]} con avvisi" },
            };
        }

        // Mixed case: some with warnings, some without.
        return new Dictionary<string, string>
        {
            { "de", $"{count} {noun["de"]} erstellt, {warningsCount} mit Warnungen" },
            { "en", $"{count} {noun["en"]} created, {warningsCount} with warnings" },
            { "fr", $"{count} {noun["fr"]}, {warningsCount} avec des avertissements" },
            { "it", $"{count} {noun["it"]}, {warningsCount} con avvisi" },
        };
    }

    private List<string> BuildIli2GpkgCommand(string containerInputPath, string containerDbFile)
    {
        var args = new List<string>
        {
            "--dbfile",
            containerDbFile,
        };

        if (import)
            args.Add("--import");

        if (schemaImport)
            args.Add("--schemaimport");

        if (createBasketCol)
            args.Add("--createBasketCol");

        if (smart2Inheritance)
            args.Add("--smart2Inheritance");

        if (disableValidation)
            args.Add("--disableValidation");

        if (models.Count > 0)
        {
            args.Add("--models");

            // ili2gpkg expects a semicolon-separated list for --models.
            args.Add(string.Join(";", models));
        }

        if (!string.IsNullOrEmpty(modelDir))
        {
            args.Add("--modeldir");
            args.Add(ContainerModelDirectory);
        }

        if (defaultSrsCode.HasValue)
        {
            args.Add("--defaultSrsCode");
            args.Add(defaultSrsCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        args.Add(containerInputPath);

        return args;
    }
}
