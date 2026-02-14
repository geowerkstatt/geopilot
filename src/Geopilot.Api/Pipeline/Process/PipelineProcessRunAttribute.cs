namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Attribute to mark methods as run methods within a pipeline process.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PipelineProcessRunAttribute : Attribute
{
}
