namespace Geopilot.Api.Validation
{
    public interface IValidationJobStore
    {
        ValidationJob? GetJob(Guid jobId);

        void AddJob(ValidationJob job);
    }
}
