using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation.Interlis;
using System.Collections.Immutable;
using System.Net.Http;

namespace Geopilot.Api.Validation.JrgDemoValidators;

public class IfcValidator : IValidator
{
    public string Name => "IFC";

    private const string SampleLogsPath = "SampleLogs/BIM-ifc";
    private static readonly ICollection<string> supportedFileExtensions = new List<string> { ".ifc" };

    private IFileProvider? fileProvider;
    private string? tempFileName;
    private string? originalFileName;

    public async Task<ValidatorResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (fileProvider == null) throw new InvalidOperationException("File provider has not been configured.");
        if (string.IsNullOrWhiteSpace(tempFileName)) throw new InvalidOperationException("Transfer file has not been configured.");
        if (!fileProvider.Exists(tempFileName)) throw new InvalidOperationException($"Transfer file with the specified name <{tempFileName}> not found.");

        cancellationToken.ThrowIfCancellationRequested();

        Task.Delay(3000, cancellationToken).Wait(cancellationToken);

        var baseNameWithoutExtension = Path.GetFileNameWithoutExtension(tempFileName);
        var logs = ImmutableDictionary.CreateBuilder<string, string>();

        if (string.Equals(originalFileName, "200mx200m_int_MOD_valid.ifc", StringComparison.OrdinalIgnoreCase))
        {
            await AddLogAsync(logs, "happy/valid.log", $"{baseNameWithoutExtension}_log.log", "Log", cancellationToken).ConfigureAwait(false);

            return new ValidatorResult(
                ValidatorResultStatus.Completed,
                "Validation réussie.",
                logs.ToImmutable());
        }

        if (string.Equals(originalFileName, "BIM_SampleBuilding_error.ifc", StringComparison.OrdinalIgnoreCase))
        {
            await AddLogAsync(logs, "error/valid.log", $"{baseNameWithoutExtension}_log.log", "Log", cancellationToken).ConfigureAwait(false);

            return new ValidatorResult(
                ValidatorResultStatus.Failed,
                "La validation a détecté des erreurs.",
                logs.ToImmutable());
        }

        return new ValidatorResult(
            ValidatorResultStatus.Failed,
            $"Une erreur inattendue s'est produite lors de la validation de <{originalFileName}>.");
    }

    private async Task AddLogAsync(
        ImmutableDictionary<string, string>.Builder logBuilder,
        string relativeSourceSampleFile,
        string destinationFileName,
        string logKey,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, SampleLogsPath, relativeSourceSampleFile);
        await using var source = File.OpenRead(sourcePath);
        await using var dest = fileProvider!.CreateFile(destinationFileName);
        await source.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
        logBuilder[logKey] = destinationFileName;
    }

    public void Configure(IFileProvider fileProvider, string tempFileName, string originalFileName)
    {
        this.fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        this.tempFileName = tempFileName ?? throw new ArgumentNullException(nameof(tempFileName));
        this.originalFileName = originalFileName ?? throw new ArgumentNullException(nameof(originalFileName));
    }

    public Task<ICollection<string>> GetSupportedFileExtensionsAsync()
        => Task.FromResult(supportedFileExtensions);

    public Task<List<Profile>> GetSupportedProfilesAsync()
        => Task.FromResult(new List<Profile>());
}
