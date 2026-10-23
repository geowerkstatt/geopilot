using Geopilot.Api.Reactions;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Geopilot.Api.Services;

/// <inheritdoc />
internal sealed class ReactionSetValidator : IReactionSetValidator
{
    private readonly IPipelineFactory pipelineFactory;
    private readonly IOptions<DeliveryOptions> deliveryOptions;

    public ReactionSetValidator(IPipelineFactory pipelineFactory, IOptions<DeliveryOptions> deliveryOptions)
    {
        this.pipelineFactory = pipelineFactory;
        this.deliveryOptions = deliveryOptions;
    }

    /// <inheritdoc />
    public ReactionSetCheck Check(string? reactionSetId, IReadOnlyDictionary<string, Dictionary<string, JsonElement>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrWhiteSpace(reactionSetId))
            return new ReactionSetCheck([], null, []);

        var set = pipelineFactory.ReactionSets.FirstOrDefault(candidate => candidate.Id == reactionSetId);
        if (set is null)
            return new ReactionSetCheck([], $"Reaction set <{reactionSetId}> does not exist.", []);

        if (!deliveryOptions.Value.DeleteWindowCloses)
        {
            return new ReactionSetCheck(
                [],
                "A reaction set needs a delete window that closes, because its reactions only run once the uploader can no longer delete the delivery. Set Delivery:DeleteDuration or Delivery:DeleteRestrictInterval.",
                []);
        }

        var declaredValues = DeclaredValues(set, values);
        var valueErrors = pipelineFactory.ValidateMandateValues(set.Id, declaredValues.ToDictionary(entry => entry.Key, entry => entry.Value.ToParameterization()));
        return new ReactionSetCheck(declaredValues, null, valueErrors);
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> DeclaredValues(
        ReactionSetConfig set,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> values)
    {
        var declaredValues = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var reaction in set.Transfers)
        {
            if (!values.TryGetValue(reaction.Id, out var reactionValues))
                continue;

            var declared = (reaction.MandateConfig ?? [])
                .Where(entry => reactionValues.ContainsKey(entry.Key))
                .ToDictionary(entry => entry.Key, entry => reactionValues[entry.Key]);
            if (declared.Count > 0)
                declaredValues[reaction.Id] = declared;
        }

        return declaredValues;
    }
}
