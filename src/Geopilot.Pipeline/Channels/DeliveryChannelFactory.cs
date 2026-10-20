using Geopilot.Pipeline.Config;
using System.Reflection;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Resolves the channel of a reaction and binds its single public constructor by the same rules as a process. A value
/// comes from exactly one of three places: the host (<c>Pipeline:ChannelConfigs:&lt;implementation&gt;</c>), the set
/// (<c>config</c> of the reaction) or the mandate (<c>mandate_config</c>), the last only for parameters marked
/// <see cref="MandateValueAttribute"/>.
/// </summary>
internal sealed class DeliveryChannelFactory
{
    private const string Subject = "Channel";
    private const string HostPlace = "Pipeline:ChannelConfigs";
    private const string SetPlace = "config";
    private const string MandatePlace = "mandate_config";

    private readonly IReadOnlyDictionary<string, Parameterization>? hostConfigs;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryChannelFactory"/> class.
    /// </summary>
    /// <param name="hostConfigs">The host values per channel implementation, or <see langword="null"/> without a
    /// host layer, which leaves the values unchecked that only the host could provide.</param>
    public DeliveryChannelFactory(IReadOnlyDictionary<string, Parameterization>? hostConfigs)
    {
        this.hostConfigs = hostConfigs;
    }

    /// <summary>
    /// Checks a reaction without constructing its channel: the implementation is a channel with one public
    /// constructor, every key of the set is a parameter and every declared mandate key a parameter marked as mandate
    /// value, no key is set in two places, every value converts to its parameter, and, with a host layer, every
    /// parameter that needs a value gets one.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="config">The values the set fixes in <c>config</c>.</param>
    /// <param name="mandateKeys">The keys the set declares in <c>mandate_config</c>.</param>
    /// <returns>One message per problem, empty when the reaction is valid.</returns>
    public IReadOnlyList<string> Validate(string implementation, Parameterization? config, IReadOnlyCollection<string> mandateKeys)
    {
        ArgumentNullException.ThrowIfNull(mandateKeys);
        config ??= new Parameterization();

        ConstructorInfo constructor;
        try
        {
            constructor = GetConstructor(implementation);
        }
        catch (InvalidOperationException ex)
        {
            return [ex.Message];
        }

        var parameters = constructor.GetParameters();
        var parameterNames = parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        var mandateParameters = parameters.Where(IsMandateValue).Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        var host = hostConfigs is null ? null : HostConfig(implementation);

        var errors = config.Keys
            .Where(key => !parameterNames.Contains(key))
            .Select(key => $"Channel <{implementation}>: the config key '{key}' is not a constructor parameter.")
            .Concat(mandateKeys
                .Where(key => !mandateParameters.Contains(key))
                .Select(key => $"Channel <{implementation}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue]."))
            .Concat(Collisions(implementation, host, config, mandateKeys))
            .ToList();

        foreach (var parameter in parameters)
        {
            // The mandate supplies a declared value; it is checked when the mandate is saved.
            if (mandateKeys.Contains(parameter.Name ?? string.Empty))
                continue;

            try
            {
                if (ConstructorBinding.TryGetConfiguredValue(parameter, config, Subject, out _))
                    continue;

                // Without a host layer, a value nobody else provides may still come from there.
                if (host is null || ConstructorBinding.TryGetConfiguredValue(parameter, host, Subject, out _) || ConstructorBinding.IsNullable(parameter))
                    continue;

                errors.Add(IsMandateValue(parameter)
                    ? $"Channel <{implementation}>: the parameter '{parameter.Name}' has no value. Set it in {SetPlace} or declare it in {MandatePlace}."
                    : MissingValueMessage(parameter));
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }
        }

        return errors;
    }

    /// <summary>
    /// Creates the channel for one attempt. The host values are read anew every time, so a changed password applies
    /// to reactions that are already waiting.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="config">The values the set fixed in <c>config</c>.</param>
    /// <param name="mandateValues">The values the mandate supplied; only those for parameters marked as mandate
    /// values are used.</param>
    /// <exception cref="InvalidOperationException">The channel cannot be resolved or a value is missing.</exception>
    public IDeliveryChannel Create(string implementation, Parameterization? config, Parameterization? mandateValues)
    {
        var constructor = GetConstructor(implementation);
        var host = HostConfig(implementation);
        var set = config ?? new Parameterization();
        var mandate = mandateValues ?? new Parameterization();
        var none = new Parameterization();

        // The host comes first: where a channel writes and with which credentials is its decision.
        var arguments = constructor.GetParameters()
            .Select(parameter => Bind(parameter, host, set, IsMandateValue(parameter) ? mandate : none))
            .ToArray();
        return (IDeliveryChannel)constructor.Invoke(arguments);
    }

    private static ConstructorInfo GetConstructor(string implementation)
    {
        // A channel implements an internal interface, so only this assembly and the ones it shares its internals with
        // can provide one; looking through the loaded assemblies cannot pick up anything else.
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(implementation))
            .FirstOrDefault(candidate => candidate is not null)
            ?? throw new InvalidOperationException($"Channel <{implementation}> is unknown.");

        if (!typeof(IDeliveryChannel).IsAssignableFrom(type))
            throw new InvalidOperationException($"<{implementation}> is not a delivery channel.");

        return ConstructorBinding.GetSingleConstructor(type, implementation, Subject);
    }

    private static IEnumerable<string> Collisions(string implementation, Parameterization? host, Parameterization config, IReadOnlyCollection<string> mandateKeys)
    {
        var places = new (string Place, IEnumerable<string> Keys)[]
        {
            (HostPlace, host?.Keys ?? Enumerable.Empty<string>()),
            (SetPlace, config.Keys),
            (MandatePlace, mandateKeys),
        };

        return places
            .SelectMany(entry => entry.Keys.Select(key => (Key: key, entry.Place)))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"Channel <{implementation}>: the key '{group.Key}' is set in {string.Join(" and ", group.Select(entry => entry.Place))}. " +
                $"A key belongs to exactly one of {HostPlace}, {SetPlace} and {MandatePlace}.");
    }

    private static bool IsMandateValue(ParameterInfo parameter) => parameter.IsDefined(typeof(MandateValueAttribute));

    private static object? Bind(ParameterInfo parameter, params Parameterization[] places)
    {
        foreach (var values in places)
        {
            if (ConstructorBinding.TryGetConfiguredValue(parameter, values, Subject, out var value))
                return value;
        }

        if (ConstructorBinding.IsNullable(parameter))
            return null;

        throw new InvalidOperationException(MissingValueMessage(parameter));
    }

    private static string MissingValueMessage(ParameterInfo parameter)
        => $"Channel initialization: No suitable parameter found for parameter of type <{parameter.ParameterType.Name}> and name <{parameter.Name}>. Parameter is not nullable, cannot initialize channel.";

    private Parameterization HostConfig(string implementation)
        => hostConfigs?.GetValueOrDefault(implementation) ?? new Parameterization();
}
