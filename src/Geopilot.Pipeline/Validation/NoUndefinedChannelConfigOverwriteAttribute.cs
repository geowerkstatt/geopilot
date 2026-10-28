using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Validation;

/// <summary>
/// Lets a reaction overwrite only keys the default configuration of its channel defines, as
/// <see cref="NoUndefinedBaseConfigOverwriteAttribute"/> does for the steps. A reaction whose channel cannot be
/// resolved is left to <see cref="ValidChannelReferenceAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class NoUndefinedChannelConfigOverwriteAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return new ValidationResult("validation object is not of type PipelineProcessConfig");
        }

        var errorMessages = new List<string>();
        foreach (var set in pipelineProcessConfig.ReactionSets ?? [])
        {
            foreach (var reaction in set.AllReactions())
            {
                var channel = pipelineProcessConfig.Channels?.Find(candidate => candidate.Id == reaction.ChannelId);
                if (channel == null || reaction.ChannelConfigOverwrites == null)
                    continue;

                var defaultConfig = channel.DefaultConfig ?? new Parameterization();
                errorMessages.AddRange(reaction.ChannelConfigOverwrites.Keys
                    .Where(key => !defaultConfig.ContainsKey(key))
                    .Select(key => $"Reaction '{reaction.Id}' in reaction set '{set.Id}' is trying to overwrite channel config parameter '{key}' which is not defined in the default config."));
            }
        }

        return errorMessages.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(string.Join(Environment.NewLine, errorMessages));
    }
}
