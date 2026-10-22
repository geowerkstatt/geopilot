using Geopilot.Pipeline.Config;
using System.Text.Json;

namespace Geopilot.Api.Reactions;

/// <summary>
/// Puts the values a mandate supplies to a reaction into the shape a channel binds from.
/// </summary>
internal static class MandateValueExtensions
{
    /// <summary>
    /// Copies the values a mandate supplies to one reaction into a <see cref="Parameterization"/>.
    /// </summary>
    internal static Parameterization ToParameterization(this IReadOnlyDictionary<string, JsonElement> values)
    {
        var parameterization = new Parameterization();
        foreach (var (key, value) in values)
        {
            parameterization[key] = value;
        }

        return parameterization;
    }
}
