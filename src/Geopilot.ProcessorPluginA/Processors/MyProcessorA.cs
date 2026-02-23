using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.ProcessorPluginA.Processors;

/// <summary>
/// Provides a processor for pipeline operations that initializes configuration and produces output values based on the
/// configured settings.
/// </summary>
public class MyProcessorA
{
    private const string OutputA = "output_a";
    private const string OutputB = "output_b";

    private const string ConfigKeyA = "config_a";
    private const string ConfigKeyB = "config_b";

    private Dictionary<string, string>? config;

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="config">A dictionary containing configuration key-value pairs to be used for initialization. Cannot be null.</param>
    [PipelineProcessInitialize]
    public void Initialize(Dictionary<string, string> config)
    {
        this.config = config;
    }

    /// <summary>
    /// Executes the pipeline process and retrieves the output values as a dictionary.
    /// </summary>
    /// <returns>A dictionary containing output values for the pipeline process. Each key represents an output name, and the
    /// corresponding value is the output value. If configuration is not available, the values will be empty strings.</returns>
    [PipelineProcessRun]
    public async Task<Dictionary<string, object>> PipelineProcessRun()
    {
        if (this.config != null)
        {
            return new Dictionary<string, object>()
            {
                { OutputA, this.config[ConfigKeyA] },
                { OutputB, this.config[ConfigKeyB] },
            };
        }
        else
        {
            return new Dictionary<string, object>()
            {
                { OutputA, "" },
                { OutputB, "" },
            };
        }
    }
}
