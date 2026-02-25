using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Services;

/// <inheritdoc/>
public class MandateService : IMandateService
{
    private readonly Context context;
    private readonly IValidationJobStore jobStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateService"/> class.
    /// </summary>
    public MandateService(Context context, IValidationJobStore jobStore)
    {
        this.context = context;
        this.jobStore = jobStore;
    }

    /// <inheritdoc/>
    public async Task<List<Mandate>> GetMandatesAsync(User? user = null, Guid? jobId = null)
    {
        return await GetMandatesQuery(user, jobId).ToListAsync();
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

    private IQueryable<Mandate> GetMandatesQuery(User? user = null, Guid? jobId = null)
    {
        var mandates = context.MandatesWithIncludes.AsNoTracking();

        if (user != null)
            mandates = FilterMandatesByUser(mandates, user);

        if (jobId != null)
            mandates = FilterMandatesByJob(mandates, jobId.Value);

        return mandates;
    }

    private IQueryable<Mandate> FilterMandatesByJob(IQueryable<Mandate> mandates, Guid jobId)
    {
        var job = jobStore.GetJob(jobId) ?? throw new ArgumentException($"Validation job with id <{jobId}> not found.", nameof(jobId));

        if (job.OriginalFileName != null)
        {
            return mandates.FilterMandatesByFileExtension(Path.GetExtension(job.OriginalFileName));
        }

        if (job.CloudFiles is { Count: > 0 })
        {
            foreach (var extension in job.CloudFiles.Select(f => Path.GetExtension(f.FileName)).Distinct())
            {
                mandates = mandates.FilterMandatesByFileExtension(extension);
            }

            return mandates;
        }

        throw new InvalidOperationException($"Validation job with id <{jobId}> has no file associated.");
    }

    private IQueryable<Mandate> FilterMandatesByUser(IQueryable<Mandate> mandates, User user)
        {
            return mandates
            .Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
    }
}
