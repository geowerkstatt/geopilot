namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Provides methods to start validation jobs and access status information for a specific job.
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly IFileProvider fileProvider;
        private readonly IValidationRunner validationRunner;
        private readonly IEnumerable<IValidator> validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationService"/> class.
        /// </summary>
        public ValidationService(IFileProvider fileProvider, IValidationRunner validationRunner, IEnumerable<IValidator> validators)
        {
            this.fileProvider = fileProvider;
            this.validationRunner = validationRunner;
            this.validators = validators;
        }

        /// <inheritdoc/>
        public (ValidationJob ValidationJob, FileHandle FileHandle) CreateValidationJob(string originalFileName)
        {
            var id = Guid.NewGuid();
            fileProvider.Initialize(id);

            var extension = Path.GetExtension(originalFileName);
            var fileHandle = fileProvider.CreateFileWithRandomName(extension);
            var validationJob = new ValidationJob(id, originalFileName, fileHandle.FileName);
            return (validationJob, fileHandle);
        }

        /// <inheritdoc/>
        public async Task<ValidationJobStatus> StartValidationJobAsync(ValidationJob validationJob)
        {
            await validationRunner.EnqueueJobAsync(validationJob, validators);
            return GetJobStatus(validationJob.Id) ?? throw new InvalidOperationException("The validation job was not enqueued.");
        }

        /// <inheritdoc/>
        public ValidationJobStatus? GetJobStatus(Guid jobId)
        {
            return validationRunner.GetJobStatus(jobId);
        }
    }
}
