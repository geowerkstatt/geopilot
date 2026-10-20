using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Validation;

/// <summary>
/// Requires every reaction of <c>delivery_processing</c> to reference a channel defined under <c>channels</c>, as
/// <see cref="ValidProcessReferenceAttribute"/> does for the steps and their processes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ValidChannelReferenceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return new ValidationResult("validation object is not of type PipelineProcessConfig");
        }

        var channelIds = (pipelineProcessConfig.Channels ?? []).Select(channel => channel.Id).ToHashSet(StringComparer.Ordinal);
        var missingChannelReferences = (pipelineProcessConfig.DeliveryProcessing ?? [])
            .SelectMany(set => set.Transfers ?? [])
            .Select(reaction => reaction.ChannelId)
            .Where(channelId => !string.IsNullOrEmpty(channelId) && !channelIds.Contains(channelId))
            .ToList();

        return missingChannelReferences.Count == 0
            ? ValidationResult.Success
            : new ValidationResult($"One or more reactions reference a channel that is not defined in the channels collection: {string.Join(", ", missingChannelReferences)}.");
    }
}
