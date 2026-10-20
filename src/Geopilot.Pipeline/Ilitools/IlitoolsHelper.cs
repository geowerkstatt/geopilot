using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Ilitools;

internal static class IlitoolsHelper
{
    /// <summary>
    /// What ilivalidator writes into its log when it cannot resolve the configured meta configuration. It then exits
    /// with the same code as a failed validation, so recognizing this line is what keeps a broken configuration from
    /// being reported as invalid data. Pinned by <c>IlivalidatorClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal const string IlivalidatorMetaConfigNotFoundMarker = "failed to get local copy of meta-config file";

    /// <summary>
    /// The same line as <see cref="IlivalidatorMetaConfigNotFoundMarker"/> as ili2gpkg words it, without the hyphen.
    /// Pinned by <c>Ili2GpkgClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal const string Ili2gpkgMetaConfigNotFoundMarker = "failed to get local copy of meta config file";

    /// <summary>
    /// What ili2gpkg writes into its log when it cannot resolve the configured <c>--validConfig</c>, e.g.
    /// <c>file ilidata:foo not found</c>. Naming the reference keeps it apart from a model that could not be fetched.
    /// Pinned by <c>Ili2GpkgClientIntegrationTest</c> against the installed tool.
    /// </summary>
    internal static string ValidConfigNotFoundMarker(string validConfig) => $"file {validConfig} not found";

    /// <summary>
    /// What the tool writes when it cannot evaluate a check. It skips the check instead of failing, so the run
    /// reports success although the check never ran, and no option turns that into an error. The usual cause is a
    /// function whose plugin is not loaded. Read from iox-ili 1.24.4, bundled in ilivalidator 1.15.0: five messages
    /// end in "is not yet implemented", three naming the constraint type (MandatoryConstraint, set constraint,
    /// uniqueness constraint) and two generic ones for a function and an expression. Matching the common substring
    /// is what covers all of them.
    /// </summary>
    internal const string CheckNotEvaluatedMarker = "is not yet implemented";

    private const string IlidataPrefix = "ilidata:";

    /// <summary>
    /// Splits a semicolon separated list, dropping blanks around the entries and empty entries. Returns null if
    /// nothing is left, so the tool keeps its own default. A single scalar value is what makes such a parameter
    /// usable in the appsettings base configuration, where a pipeline definition cannot override it and only scalars
    /// are carried. For the repositories the separator is the one the tool itself uses for <c>--modeldir</c> and the
    /// wrapper rejects an entry containing it; a plugin id or a model name cannot contain it either. So nothing is
    /// lost by splitting here.
    /// </summary>
    internal static string[]? SplitConfiguredList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var entries = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return entries.Length > 0 ? entries : null;
    }

    /// <summary>
    /// Turns a configured dataset id into the <c>ilidata:</c> reference the tool expects. An existing prefix is kept.
    /// Returns null for a blank value.
    /// </summary>
    internal static string? ToIlidataReference(string? datasetId)
    {
        datasetId = datasetId?.Trim();
        if (string.IsNullOrEmpty(datasetId))
            return null;

        // The tool resolves the profile through the repository index, so the configured value is a dataset id.
        return datasetId.StartsWith(IlidataPrefix, StringComparison.OrdinalIgnoreCase)
            ? datasetId
            : IlidataPrefix + datasetId;
    }

    /// <summary>
    /// Throws if the log of <paramref name="tool"/> shows that the run is not a verdict about the data: the
    /// <paramref name="metaConfig"/> or <paramref name="validConfig"/> it was given (<c>ilidata:</c> references,
    /// null if none) could not be resolved, or a check was skipped.
    /// </summary>
    public static async Task AssertNoUndefinedValidationOutcome(Ilitool tool, IPipelineFile errorLog, bool success, string? metaConfig, string? validConfig, IReadOnlyList<string>? modelDirs, IReadOnlyList<string>? pluginIds, CancellationToken cancellationToken)
    {
        var logFindings = await ScanLogAsync(errorLog, MetaConfigNotFoundMarker(tool), validConfig, cancellationToken);
        var toolName = tool.ToString().ToLowerInvariant();
        var repositories = modelDirs is { Count: > 0 }
            ? string.Join(';', modelDirs)
            : "the default repositories of the tool";

        if (!success && metaConfig != null && logFindings.UnresolvedMetaConfig)
        {
            throw new InvalidOperationException(
                $"The meta configuration <{metaConfig}> could not be resolved from <{repositories}>, so {toolName} did not process the data with it. Check the {MetaConfigSettingName(tool)} and modelDirs configuration of this process.");
        }

        if (!success && logFindings.UnresolvedValidConfig)
        {
            throw new InvalidOperationException(
                $"The validation configuration <{validConfig}> could not be resolved from <{repositories}>, so {toolName} did not validate the data with it. Check the validConfig and modelDirs configuration of this process.");
        }

        // A skipped check is not a verdict about the data, whether the rest of the run passed or failed. Reported
        // as an error rather than as an unsuccessful validation, because the cause is the configuration and not
        // the delivery: the tool would otherwise let a green result stand for a check that never ran.
        if (logFindings.CheckNotEvaluated)
        {
            var plugins = pluginIds is { Count: > 0 }
                ? string.Join(';', pluginIds)
                : "none";
            throw new InvalidOperationException(
                $"{toolName} skipped a check it could not evaluate, so its result says nothing about that check. The usual cause is a user defined function whose plugin is not loaded; the configured plugins are <{plugins}>. The log names the check that was skipped.");
        }
    }

    private static string MetaConfigNotFoundMarker(Ilitool tool) => tool switch
    {
        Ilitool.Ilivalidator => IlivalidatorMetaConfigNotFoundMarker,
        Ilitool.Ili2gpkg => Ili2gpkgMetaConfigNotFoundMarker,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
    };

    /// <summary>
    /// The process setting the meta configuration comes from, so the message points at what to fix.
    /// </summary>
    private static string MetaConfigSettingName(Ilitool tool) => tool switch
    {
        Ilitool.Ilivalidator => "validationProfile",
        Ilitool.Ili2gpkg => "metaConfig",
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
    };

    /// <summary>
    /// The things the tool only says in its log, each of which means the run is not a verdict about the data.
    /// </summary>
    private sealed record LogFindings(bool UnresolvedMetaConfig, bool UnresolvedValidConfig, bool CheckNotEvaluated);

    private static async Task<LogFindings> ScanLogAsync(IPipelineFile logFile, string metaConfigMarker, string? validConfig, CancellationToken cancellationToken)
    {
        var path = await logFile.GetLocalPathAsync(cancellationToken);
        if (!File.Exists(path))
            return new LogFindings(false, false, false);

        var validConfigMarker = validConfig is null ? null : ValidConfigNotFoundMarker(validConfig);
        var unresolvedMetaConfig = false;
        var unresolvedValidConfig = false;
        var checkNotEvaluated = false;

        using var reader = new StreamReader(path);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            unresolvedMetaConfig |= line.Contains(metaConfigMarker, StringComparison.Ordinal);
            unresolvedValidConfig |= validConfigMarker != null && line.Contains(validConfigMarker, StringComparison.Ordinal);
            checkNotEvaluated |= line.Contains(CheckNotEvaluatedMarker, StringComparison.Ordinal);
        }

        return new LogFindings(unresolvedMetaConfig, unresolvedValidConfig, checkNotEvaluated);
    }
}
