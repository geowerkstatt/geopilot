namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Attribute to mark methods for initialization within a pipeline process.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PipelineProcessInitializeAttribute : Attribute
{
}
