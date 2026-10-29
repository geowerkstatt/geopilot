using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.Gpkg;

/// <summary>
/// Reads metadata of a GeoPackage file, such as its models.
/// </summary>
internal partial class GpkgMetadataExtractorProcess(ILogger logger)
{
    private static readonly LocalizedText SuccessStatusMessageFormat = new Dictionary<string, string>
    {
        { "de", "Modelle aus der GeoPackage-Datei gelesen: {0}." },
        { "fr", "Modèles lus dans le fichier GeoPackage : {0}." },
        { "it", "Modelli letti dal file GeoPackage: {0}." },
        { "en", "Models read from the GeoPackage file: {0}." },
    };

    private static readonly LocalizedText MissingModelsStatusMessage = new Dictionary<string, string>
    {
        { "de", "Keine Modelle in der GeoPackage-Datei gefunden." },
        { "fr", "Aucun modèle trouvé dans le fichier GeoPackage." },
        { "it", "Nessun modello trovato nel file GeoPackage." },
        { "en", "No models found in the GeoPackage file." },
    };

    private static readonly LocalizedText UnreadableStatusMessage = new Dictionary<string, string>
    {
        { "de", "Die GeoPackage-Datei konnte nicht gelesen werden." },
        { "fr", "Le fichier GeoPackage n'a pas pu être lu." },
        { "it", "Il file GeoPackage non può essere letto." },
        { "en", "The GeoPackage file could not be read." },
    };

    private static readonly GpkgMetadataExtractorResult MissingDataResult = new()
    {
        Models = [],
        FileState = GpkgFileState.MissingData,
        StatusMessage = MissingModelsStatusMessage,
    };

    private static readonly GpkgMetadataExtractorResult UnreadableResult = new()
    {
        Models = [],
        FileState = GpkgFileState.Unreadable,
        StatusMessage = UnreadableStatusMessage,
    };

    /// <summary>
    /// Reads the metadata from the provided <paramref name="gpkgFile"/>.
    /// </summary>
    /// <param name="gpkgFile">The GeoPackage file to read metadata from.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="GpkgMetadataExtractorResult"/> containing the extracted metadata.</returns>
    [PipelineProcessRun]
    public async Task<GpkgMetadataExtractorResult> RunAsync(IPipelineFile gpkgFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gpkgFile);

        logger.LogInformation("Extracting metadata from GPKG file <{FileName}>...", gpkgFile.OriginalFileName);

        IReadOnlyList<string> rawModels;
        try
        {
            var localPath = await gpkgFile.GetLocalPathAsync(cancellationToken);
            await using var connection = new SqliteConnection(BuildConnectionString(localPath));
            await connection.OpenAsync(cancellationToken);

            if (!await HasModelTableAsync(connection, cancellationToken))
            {
                logger.LogInformation("The required table T_ILI2DB_MODEL is missing in the GPKG file <{FileName}>", gpkgFile.OriginalFileName);
                return MissingDataResult;
            }

            rawModels = await ReadModelsAsync(connection, cancellationToken);
        }
        catch (SqliteException ex)
        {
            logger.LogInformation("Failed to read models from the GPKG file <{FileName}>: {Message}", gpkgFile.OriginalFileName, ex.Message);
            return UnreadableResult;
        }

        if (rawModels.Count == 0)
        {
            logger.LogInformation("No models found in the GPKG file <{FileName}>", gpkgFile.OriginalFileName);
            return MissingDataResult;
        }

        var modelNames = ExtractModelNames(rawModels);
        if (!modelNames.All(IsValidInterlisModelName))
        {
            logger.LogInformation("The GPKG file <{FileName}> contains invalid interlis model names", gpkgFile.OriginalFileName);
            return UnreadableResult;
        }

        var modelsDisplayString = string.Join(", ", modelNames);
        logger.LogInformation("Found {Count} model(s) in the GPKG file <{FileName}>", modelNames.Count, gpkgFile.OriginalFileName);
        return new GpkgMetadataExtractorResult
        {
            Models = modelNames.ToArray(),
            FileState = GpkgFileState.Readable,
            StatusMessage = SuccessStatusMessageFormat.Map(format => string.Format(CultureInfo.InvariantCulture, format, modelsDisplayString)),
        };
    }

    private static string BuildConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadOnly,

        // Pooling off: a pooled connection keeps the file open after disposal, so the pipeline could not delete its working directory.
        Pooling = false,
    }.ToString();

    private static async Task<bool> HasModelTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'T_ILI2DB_MODEL' COLLATE NOCASE";
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task<IReadOnlyList<string>> ReadModelsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var models = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT modelName FROM T_ILI2DB_MODEL WHERE modelName IS NOT NULL";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            models.Add(reader.GetString(0));
        }

        return models;
    }

    internal static IReadOnlySet<string> ExtractModelNames(IReadOnlyList<string> rawModelNames)
    {
        var modelNames = new HashSet<string>();
        foreach (var rawModelName in rawModelNames)
        {
            // Remove any references in the model name (e.g. "ModelA{ModelB} ModelC{ModelD ModelE}" becomes "ModelA ModelC")
            var rawModelNameWithoutReferences = ReferencedModelsRegex().Replace(rawModelName, string.Empty);
            foreach (var modelName in rawModelNameWithoutReferences.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                modelNames.Add(modelName);
            }
        }

        return modelNames;
    }

    internal static bool IsValidInterlisModelName(string modelName)
    {
        return InterlisNameRegex().IsMatch(modelName);
    }

    [GeneratedRegex("{[^}]*}", RegexOptions.NonBacktracking)]
    private static partial Regex ReferencedModelsRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*\z", RegexOptions.NonBacktracking)]
    private static partial Regex InterlisNameRegex();
}
