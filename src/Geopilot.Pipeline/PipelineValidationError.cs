namespace Geopilot.Api.Pipeline;

internal class PipelineValidationError
{
    internal Type SourceObject { get; }

    internal IEnumerable<string> MemberNames { get; }

    internal string Message { get; }

    internal PipelineValidationError(Type sourceObject, IEnumerable<string> memberNames, string message)
    {
        this.Message = message;
        this.MemberNames = memberNames;
        this.SourceObject = sourceObject;
    }
}
