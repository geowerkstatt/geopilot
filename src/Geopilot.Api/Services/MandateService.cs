using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.EntityFrameworkCore;

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
    public async Task<List<MandateSummary>> GetMandateSummariesAsync(User? user, Guid uploadId)
    {
        var mandates = context.Mandates.AsNoTracking();
        mandates = FilterMandatesByUser(mandates, user);
        mandates = FilterMandatesByUpload(mandates, uploadId);
        return await mandates.ToSummaries().ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Mandate?> GetMandateForUser(int mandateId, User? user)
    {
        var mandates = FilterMandatesByUser(context.Mandates.AsNoTracking(), user);
        return await mandates.SingleOrDefaultAsync(m => m.Id == mandateId);
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
    {
        return user == null
            ? mandates.Where(m => m.IsPublic)
            : mandates.Where(m => m.IsPublic || m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
    }

    private IQueryable<Mandate> FilterMandatesByUpload(IQueryable<Mandate> mandates, Guid uploadId)
    {
        var upload = uploadStore.GetUpload(uploadId) ?? throw new ArgumentException($"Upload with id <{uploadId}> not found.", nameof(uploadId));

        var fileExtensions = upload.Files
            .Select(f => Path.GetExtension(f.FileName))
            .Where(ext => !string.IsNullOrEmpty(ext))
            .Distinct()
            .ToList();

        if (fileExtensions.Count == 0)
            throw new InvalidOperationException($"Upload with id <{uploadId}> has no file associated.");

        var pipelineIds = pipelineService.GetAvailablePipelines().Select(p => p.Id).ToHashSet();
        mandates = mandates.Where(m => !string.IsNullOrEmpty(m.PipelineId) && pipelineIds.Contains(m.PipelineId));

        foreach (var extension in fileExtensions)
        {
            mandates = mandates.FilterMandatesByFileExtension(extension);
        }

        return mandates;
    }
}
