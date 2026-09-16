using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Geopilot.Api.Services;

/// <inheritdoc/>
public class MandateService : IMandateService
{
    private readonly Context context;
    private readonly IUploadStore uploadStore;
    private readonly IPipelineService pipelineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateService"/> class.
    /// </summary>
    public MandateService(Context context, IUploadStore uploadStore, IPipelineService pipelineService)
    {
        this.context = context;
        this.uploadStore = uploadStore;
        this.pipelineService = pipelineService;
    }

    /// <inheritdoc/>
    public async Task<List<Mandate>> GetMandatesAsync()
    {
        return await context.MandatesWithIncludes
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<MandateSummary>> GetMandateSummariesAsync(User? user, Guid? uploadId)
    {
        var mandates = context.Mandates.AsNoTracking();
        mandates = FilterMandatesByUser(mandates, user);
        mandates = FilterMandatesByResolvablePipeline(mandates);

        if (uploadId.HasValue)
            mandates = FilterMandatesByUpload(mandates, uploadId.Value);

        return await mandates.ToSummaries().ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetMandateKeysAsync()
    {
        return await context.Mandates
            .Where(m => m.Key != null)
            .Select(m => m.Key!)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Mandate?> GetMandateForDeclarerAsync(int mandateId, Declarer? declarer)
    {
        var mandates = FilterMandatesByDeclarer(context.Mandates.AsNoTracking(), declarer);
        return await mandates.SingleOrDefaultAsync(m => m.Id == mandateId);
    }

    /// <inheritdoc/>
    public async Task<Mandate?> GetMandateByKeyAsync(string key, Declarer? declarer)
    {
        // An empty key must not match: EF turns a null comparison into "every mandate without a key", which would
        // make SingleOrDefault throw as soon as a second one exists.
        if (string.IsNullOrWhiteSpace(key))
            return null;

        // Trimmed like MandateController trims it on the way in, so a key that came from a configuration file
        // or an environment variable does not miss its mandate over a trailing space. The comparison itself
        // stays exact, case included.
        var normalizedKey = key.Trim();
        var mandates = FilterMandatesByDeclarer(context.Mandates.AsNoTracking(), declarer);
        return await mandates.SingleOrDefaultAsync(m => m.Key == normalizedKey);
    }

    /// <inheritdoc/>
    public async Task<MandateDeliverability> GetDeliverabilityAsync(Mandate mandate, Guid? uploadId)
    {
        ArgumentNullException.ThrowIfNull(mandate);

        if (!mandate.AllowDelivery)
            return MandateDeliverability.DeliveryNotAllowed;

        // Run the same filters the selection list uses, narrowed to this one mandate, so both answer alike.
        var mandates = FilterMandatesByResolvablePipeline(context.Mandates.AsNoTracking().Where(m => m.Id == mandate.Id));
        if (!await mandates.AnyAsync())
            return MandateDeliverability.PipelineNotConfigured;

        if (uploadId.HasValue && !await FilterMandatesByUpload(mandates, uploadId.Value).AnyAsync())
            return MandateDeliverability.FilesNotAccepted;

        return MandateDeliverability.Deliverable;
    }

    /// <inheritdoc/>
    public async Task<bool> AcceptsFileExtensionAsync(int mandateId, string fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension))
            return false;

        return await context.Mandates.AsNoTracking()
            .Where(m => m.Id == mandateId)
            .FilterMandatesByFileExtension(fileExtension)
            .AnyAsync();
    }

    /// <inheritdoc/>
    public HashSet<string> GetFileExtensionsForMandates()
    {
        return context.Mandates
            .Select(mandate => mandate.FileTypes)
            .AsEnumerable()
            .SelectMany(ext => ext)
            .Select(ext => ext.ToLowerInvariant())
            .ToHashSet();
    }

    private IQueryable<Mandate> FilterMandatesByUser(IQueryable<Mandate> mandates, User? user)
        => FilterMandatesByDeclarer(mandates, user is null ? null : Declarer.ForUser(user.Id));

    /// <summary>
    /// The one rule that decides who may deliver to a mandate: anyone for a public mandate, otherwise a member
    /// of one of its organisations, whether that member is a person or a machine client.
    /// </summary>
    private static IQueryable<Mandate> FilterMandatesByDeclarer(IQueryable<Mandate> mandates, Declarer? declarer)
    {
        if (declarer is null)
            return mandates.Where(m => m.IsPublic);

        if (declarer.MachineClientId is int clientId)
            return mandates.Where(m => m.IsPublic || m.Organisations.SelectMany(o => o.MachineClients).Any(c => c.Id == clientId));

        var userId = declarer.UserId ?? throw new UnreachableException("A declarer that is no machine client is a user.");
        return mandates.Where(m => m.IsPublic || m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == userId));
    }

    private IQueryable<Mandate> FilterMandatesByResolvablePipeline(IQueryable<Mandate> mandates)
    {
        var pipelineIds = pipelineService.GetAvailablePipelines().Select(p => p.Id).ToHashSet();
        return mandates.Where(m => !string.IsNullOrEmpty(m.PipelineId) && pipelineIds.Contains(m.PipelineId));
    }

    private IQueryable<Mandate> FilterMandatesByUpload(IQueryable<Mandate> mandates, Guid uploadId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        var fileExtensions = upload.Files
            .Select(f => Path.GetExtension(f.FileName))
            .Where(ext => !string.IsNullOrEmpty(ext))
            .Distinct()
            .ToList();

        // An upload always has files (initiation refuses an empty one), but none of them may carry an
        // extension, and without extensions there is nothing to match a mandate's file types against.
        if (fileExtensions.Count == 0)
            throw new InvalidOperationException($"Upload with id <{uploadId}> has no file with a file extension.");

        foreach (var extension in fileExtensions)
        {
            mandates = mandates.FilterMandatesByFileExtension(extension);
        }

        return mandates;
    }
}
