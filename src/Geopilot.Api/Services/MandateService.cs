using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Services;

/// <inheritdoc/>
public class MandateService : IMandateService
{
    private readonly Context context;
    private readonly IUploadStore uploadStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateService"/> class.
    /// </summary>
    public MandateService(Context context, IUploadStore uploadStore)
    {
        this.context = context;
        this.uploadStore = uploadStore;
    }

    /// <inheritdoc/>
    public async Task<List<Mandate>> GetMandatesAsync(User? user = null, Guid? uploadId = null)
    {
        return await GetMandatesQuery(user, uploadId).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Mandate?> GetMandateForUser(int mandateId, User? user)
    {
        var mandates = context.Mandates.AsNoTracking();

        if (user != null)
        {
            mandates = mandates.Where(m => m.IsPublic || m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
        }
        else
        {
            mandates = mandates.Where(m => m.IsPublic);
        }

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

    private IQueryable<Mandate> GetMandatesQuery(User? user = null, Guid? uploadId = null)
    {
        var mandates = context.MandatesWithIncludes.AsNoTracking();

        if (user == null)
        {
            mandates = mandates.Where(m => m.IsPublic);
        }
        else if (!user.IsAdmin || uploadId != null)
        {
            mandates = mandates.Where(m => m.IsPublic || m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
        }

        if (uploadId != null)
            mandates = FilterMandatesByUpload(mandates, uploadId.Value);

        return mandates;
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

        foreach (var extension in fileExtensions)
        {
            mandates = mandates.FilterMandatesByFileExtension(extension);
        }

        return mandates;
    }
}
