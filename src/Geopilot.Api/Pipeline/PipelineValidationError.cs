namespace Geopilot.Api.Pipeline;

internal class PipelineValidationError
{
    internal Type SourceObject { get; }

    internal string Message { get; }

    internal PipelineValidationError(Type sourceObject, string message)
    {
        Message = message;
        SourceObject = sourceObject;
    }
}
