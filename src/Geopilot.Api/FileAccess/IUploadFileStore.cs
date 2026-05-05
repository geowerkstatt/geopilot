namespace Geopilot.Api.FileAccess;

/// <summary>
/// Per-job file store rooted at the configured upload directory. Holds the original
/// files uploaded by the user (or staged from cloud uploads) before pipeline execution.
/// </summary>
public interface IUploadFileStore : IJobFileStore
{
}
