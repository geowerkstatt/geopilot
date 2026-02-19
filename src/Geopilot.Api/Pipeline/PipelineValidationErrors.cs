namespace Geopilot.Api.Pipeline;

internal class PipelineValidationErrors : List<PipelineValidationError>
{
    internal PipelineValidationErrors()
    {
    }

    internal PipelineValidationErrors(PipelineValidationErrors errors)
        : base(errors)
    {
    }

    internal bool HasErrors => this.Count > 0;

    internal string ErrorMessage => string.Join(Environment.NewLine, this.Select(e => $"{e.SourceObject.Name}: {e.Message}"));

    public override string ToString()
    {
        return ErrorMessage;
    }
}
