namespace Geopilot.Pipeline.Config;

/// <summary>
/// Definition of the inputs for a pipeline step.
/// Each key is the name of an input parameter and each value is the raw YAML node from the pipeline defintion.
/// </summary>
/// <remarks>
/// The raw YAML nodes are compiled into <see cref="InputValue"/>s by <see cref="InputCompiler"/> when the pipeline is built.
/// </remarks>
public class InputConfig : Dictionary<string, object?>
{
}
