#nullable disable

namespace Geopilot.Pipeline.Test.Processes;

// Built without nullable annotations, like a plugin that never enabled them, so its parameters declare nothing.
public class NoNullableAnnotationsTestProcess
{
    public NoNullableAnnotationsTestProcess(string value)
    {
        Value = value;
    }

    public string Value { get; }
}
