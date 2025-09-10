namespace Geopilot.Api.Validation;

/// <summary>
/// Runs validation jobs.
/// </summary>
public interface IValidationRunner
{
    /// <summary>
    /// Asynchronously enqueues and executes the <paramref name="validators"/> specified.
    /// </summary>
    /// <param name="validationJob">The validation job.</param>
    /// <param name="validators">The validators used to validate the <paramref name="validationJob"/>.</param>
    /// <returns></returns>
    Task EnqueueJobAsync(ValidationJob validationJob, IEnumerable<IValidator> validators);
}
