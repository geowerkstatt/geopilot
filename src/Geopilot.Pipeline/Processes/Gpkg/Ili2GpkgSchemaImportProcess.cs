using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal class Ili2GpkgSchemaImportProcess
{
    private readonly Ili2GpkgArgs ili2GpkgArgs;
    private readonly IPipelineFile? modelRepository;
    private readonly IIli2GpkgClient ili2GpkgClient;
    private readonly IPipelineFileManager pipelineFileManager;
    private readonly ILogger logger;

    private static readonly LocalizedText SuccessStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Schema-Import war erfolgreich." },
        { "fr", "L'importation du schéma s'est déroulée avec succès." },
        { "it", "L'importazione dello schema è andata a buon fine." },
        { "en", "The schema import was successful." },
    };

    private static readonly LocalizedText FailureStatusMessage = new Dictionary<string, string>
    {
        { "de", "Der Schema-Import hat Fehler ergeben. Details stehen in der Log-Datei." },
        { "fr", "L'importation du schéma a généré des erreurs. Vous trouverez plus de détails dans le fichier journal." },
        { "it", "L'importazione dello schema ha generato degli errori. I dettagli sono riportati nel file di log." },
        { "en", "The schema import resulted in errors. Details can be found in the log file." },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Ili2GpkgSchemaImportProcess"/> class.
    /// </summary>
    public Ili2GpkgSchemaImportProcess(
        string? modelDirs,
        string? toolVersion,
        bool? createBasketCol,
        bool? createFk,
        bool? createGeomIdx,
        bool? createEnumTabs,
        bool? createTidCol,
        bool? beautifyEnumDispName,
        bool? smart1Inheritance,
        bool? smart2Inheritance,
        bool? strokeArcs,
        bool? sqlEnableNull,
        bool? sqlColsAsText,
        IPipelineFile? modelRepository,
        IIli2GpkgClient ili2GpkgClient,
        IPipelineFileManager pipelineFileManager,
        ILogger logger)
    {
        this.modelRepository = modelRepository;
        this.ili2GpkgClient = ili2GpkgClient;
        this.pipelineFileManager = pipelineFileManager;
        this.logger = logger;

        ili2GpkgArgs = new Ili2GpkgArgs
        {
            ModelDirs = IlitoolsHelper.SplitConfiguredList(modelDirs),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),

            CreateBasketCol = createBasketCol ?? false,
            CreateFk = createFk ?? false,
            CreateGeomIdx = createGeomIdx ?? false,
            CreateEnumTabs = createEnumTabs ?? false,
            CreateTidCol = createTidCol ?? false,
            BeautifyEnumDispName = beautifyEnumDispName ?? false,
            Smart1Inheritance = smart1Inheritance ?? false,
            Smart2Inheritance = smart2Inheritance ?? false,
            StrokeArcs = strokeArcs ?? false,
            SqlEnableNull = sqlEnableNull ?? false,
            SqlColsAsText = sqlColsAsText ?? false,
        };
    }

    [PipelineProcessRun]
    public async Task<Ili2GpkgSchemaImportResult> RunAsync(
        IPipelineFile[] modelFiles,
        IPipelineFile? dbFile,
        string? models,
        int? defaultSrsCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modelFiles);
        if (modelFiles.Length == 0)
        {
            throw new ArgumentException("No model files provided for schema import.", nameof(modelFiles));
        }

        logger.LogInformation("Starting schema import for {ModelCount} model file(s).", modelFiles.Length);

        var args = ili2GpkgArgs with
        {
            Models = IlitoolsHelper.SplitConfiguredList(models),
            DefaultSrsCode = defaultSrsCode,
        };

        var success = true;
        IPipelineFile? outputFile = null;
        List<IPipelineFile> logFiles = new(modelFiles.Length);
        List<IPipelineFile> xtfLogFiles = new(modelFiles.Length);

        for (var i = 0; i < modelFiles.Length; i++)
        {
            // Add a suffix to intermediate schema files, keeping the final file as "schema".
            var filename = i == modelFiles.Length - 1 ? "schema" : $"schema-{i + 1}";
            outputFile = pipelineFileManager.GeneratePipelineFile(filename, "gpkg");

            // Add a suffix to the log files if there are multiple model files to distinguish logs.
            var suffix = modelFiles.Length == 1 ? "" : $"-{i + 1}-{modelFiles[i].OriginalFileNameWithoutExtension}";
            var errorLogFile = pipelineFileManager.GeneratePipelineFile("schema-import" + suffix, "log");
            var xtfLogFile = pipelineFileManager.GeneratePipelineFile("schema-import-log" + suffix, "xtf");

            var result = await ili2GpkgClient.SchemaImportAsync(
                args,
                modelFile: modelFiles[i],
                outputFile: outputFile,
                logFile: errorLogFile,
                xtfLogFile: xtfLogFile,
                inputFile: dbFile,
                modelRepositoryArchive: modelRepository,
                cancellationToken);

            logFiles.Add(errorLogFile);
            xtfLogFiles.Add(xtfLogFile);

            if (!result.Success)
            {
                success = false;
                break;
            }

            // Use the output of this import as the input of the next one.
            dbFile = outputFile;
        }

        return new Ili2GpkgSchemaImportResult
        {
            Success = success,
            StatusMessage = success ? SuccessStatusMessage : FailureStatusMessage,
            DbFile = success && outputFile != null ? [outputFile] : [],
            ErrorLogs = logFiles.ToArray(),
            XtfLogs = xtfLogFiles.ToArray(),
        };
    }
}
