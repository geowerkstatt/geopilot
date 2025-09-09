namespace Geopilot.Api.Validation
{
    public interface IValidationManager
    {
        ValidationJob? GetJob(Guid jobId);
        void AddJob(ValidationJob job);
    }
}
