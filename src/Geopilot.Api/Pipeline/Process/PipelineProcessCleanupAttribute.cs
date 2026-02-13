namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Attribute to mark a method as a cleanup method for a pipeline process.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PipelineProcessCleanupAttribute : Attribute
{
}
