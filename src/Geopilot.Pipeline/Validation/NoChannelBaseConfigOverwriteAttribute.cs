using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Validation;

/// <summary>
/// Rejects a pipeline definition that sets a channel configuration key which the hosting base configuration already
/// pins, in a channel's <c>default_config</c>, a reaction's <c>channel_config_overwrites</c> or its
/// <c>mandate_config</c>, as <see cref="NoBaseConfigOverwriteAttribute"/> does for the processes. Like there, the
/// check is skipped without a base configuration in <see cref="ValidationContext.Items"/>, and a channel without an
/// implementation is skipped because it can name no base entry.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class NoChannelBaseConfigOverwriteAttribute : ValidationAttribute
{
    /// <summary>
    /// Key under which the caller of the validation places the base configuration of every channel implementation,
    /// taken from <c>Pipeline:ChannelConfigs</c>.
    /// </summary>
    internal const string BaseConfigsKey = "ChannelBaseConfigs";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return new ValidationResult("validation object is not of type PipelineProcessConfig");
        }

        if (!validationContext.Items.TryGetValue(BaseConfigsKey, out var baseConfigsItem) ||
            baseConfigsItem is not IReadOnlyDictionary<string, Parameterization> baseConfigs ||
            baseConfigs.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errorMessages = CollectDefaultConfigCollisions(pipelineProcessConfig, baseConfigs)
            .Concat(CollectReactionCollisions(pipelineProcessConfig, baseConfigs))
            .ToList();

        return errorMessages.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(string.Join(Environment.NewLine, errorMessages));
    }

    private static IEnumerable<string> CollectDefaultConfigCollisions(
        PipelineProcessConfig pipelineProcessConfig,
        IReadOnlyDictionary<string, Parameterization> baseConfigs)
    {
        foreach (var channel in pipelineProcessConfig.Channels ?? [])
        {
            foreach (var key in KeysInBaseConfig(channel, channel.DefaultConfig?.Keys, baseConfigs))
            {
                yield return ChannelConfigCollision.InDefaultConfig(channel.Implementation, channel.Id, key);
            }
        }
    }

    private static IEnumerable<string> CollectReactionCollisions(
        PipelineProcessConfig pipelineProcessConfig,
        IReadOnlyDictionary<string, Parameterization> baseConfigs)
    {
        foreach (var set in pipelineProcessConfig.ReactionSets ?? [])
        {
            foreach (var reaction in set.Transfers ?? [])
            {
                var channel = pipelineProcessConfig.Channels?.Find(candidate => candidate.Id == reaction.ChannelId);
                if (channel == null)
                    continue;

                foreach (var key in KeysInBaseConfig(channel, reaction.ChannelConfigOverwrites?.Keys, baseConfigs))
                {
                    yield return ChannelConfigCollision.InReactionOverwrite(channel.Implementation, set.Id, reaction.Id, key);
                }

                foreach (var key in KeysInBaseConfig(channel, reaction.MandateConfig?.Select(entry => entry.Key), baseConfigs))
                {
                    yield return ChannelConfigCollision.InMandateConfig(channel.Implementation, set.Id, reaction.Id, key);
                }
            }
        }
    }

    private static IEnumerable<string> KeysInBaseConfig(
        ChannelConfig channel,
        IEnumerable<string>? keys,
        IReadOnlyDictionary<string, Parameterization> baseConfigs)
    {
        if (keys == null || string.IsNullOrEmpty(channel.Implementation) || !baseConfigs.TryGetValue(channel.Implementation, out var baseConfig))
            return [];

        // A mandate entry without a key is reported on the entry itself; this attribute runs before that recursion.
        return keys.Where(key => !string.IsNullOrEmpty(key) && baseConfig.ContainsKey(key));
    }
}
