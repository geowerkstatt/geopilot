namespace Geopilot.Pipeline.Config;

/// <summary>
/// Formats the diagnostics for a process configuration key that the pipeline definition sets although
/// the hosting base configuration (<c>Pipeline:ProcessConfigs</c>) already pins it. The two layers are
/// maintained by different people in different places, so the message names both locations and says
/// which entry to remove.
/// </summary>
internal static class ProcessConfigCollision
{
    private const string Remedy =
        "The base configuration cannot be overridden. Remove the key from the pipeline definition, " +
        "or remove it from the base configuration if the value has to be set per pipeline.";

    internal static string InDefaultConfig(string implementation, string processId, string key) =>
        $"{Preamble(implementation, key)} and in the pipeline definition " +
        $"('processes[id={processId}].default_config.{key}'). {Remedy}";

    internal static string InStepOverwrite(string implementation, string pipelineId, string stepId, string key) =>
        $"{Preamble(implementation, key)} and overwritten in the pipeline definition " +
        $"('pipelines[id={pipelineId}].steps[id={stepId}].process_config_overwrites.{key}'). {Remedy}";

    private static string Preamble(string implementation, string key) =>
        $"Process configuration collision for implementation '{implementation}': the key '{key}' is set " +
        $"in the base configuration (app settings 'Pipeline:ProcessConfigs:{implementation}:{key}', " +
        $"environment variable 'Pipeline__ProcessConfigs__{implementation}__{key}')";
}
