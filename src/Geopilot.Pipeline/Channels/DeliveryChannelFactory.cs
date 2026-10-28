using Geopilot.Pipeline.Config;
using System.Reflection;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Resolves the channel of a reaction and binds its single public constructor by the same rules as a process. A value
/// comes from exactly one of three places: the base configuration (<c>Pipeline:ChannelConfigs:&lt;implementation&gt;</c>),
/// the pipeline definition (<c>default_config</c> of the channel with the reaction's <c>channel_config_overwrites</c>)
/// or the mandate (<c>mandate_config</c>), the last only for parameters marked <see cref="MandateValueAttribute"/>.
/// A parameter marked <see cref="BaseConfigValueAttribute"/> comes from the base configuration only.
/// </summary>
internal sealed class DeliveryChannelFactory
{
    private const string Subject = "Channel";

    // The administration offers a text field, a list of texts or a switch for a mandate value, nothing else.
    private static readonly Dictionary<Type, MandateValueType> MandateValueTypes = new()
    {
        [typeof(string)] = MandateValueType.Text,
        [typeof(IReadOnlyList<string>)] = MandateValueType.TextList,
        [typeof(bool)] = MandateValueType.Switch,
        [typeof(bool?)] = MandateValueType.Switch,
    };

    private readonly IReadOnlyDictionary<string, Parameterization>? baseConfigs;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryChannelFactory"/> class.
    /// </summary>
    /// <param name="baseConfigs">The base configuration per channel implementation, or <see langword="null"/>
    /// without one, which leaves the values unchecked that only the base configuration could provide.</param>
    public DeliveryChannelFactory(IReadOnlyDictionary<string, Parameterization>? baseConfigs)
    {
        this.baseConfigs = baseConfigs;
    }

    /// <summary>
    /// Checks a reaction without constructing its channel: the implementation is a channel with one public
    /// constructor, every key of the definition is a parameter not marked as base configuration value, every declared
    /// mandate key a parameter marked as mandate value that the definition does not set, every parameter marked as
    /// mandate value takes a text, a list of texts or a switch, every value converts to its parameter, and, with a base
    /// configuration, every parameter that needs a value gets one. A key the base configuration pins is the concern
    /// of the definition validation.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="config">The values the pipeline definition sets: the default configuration of the channel with
    /// the reaction's overwrites.</param>
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
        var baseConfig = baseConfigs is null ? null : BaseConfig(implementation);

        var errors = config.Keys
            .Where(key => !parameterNames.Contains(key))
            .Select(key => $"Channel <{implementation}>: the default_config key '{key}' is not a constructor parameter.")
            .Concat(mandateKeys
                .Where(key => !mandateParameters.Contains(key))
                .Select(key => $"Channel <{implementation}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue]."))
            .Concat(mandateKeys
                .Where(config.ContainsKey)
                .Select(key => $"Channel <{implementation}>: the mandate value '{key}' is already set in default_config. A value comes either from the pipeline definition or from the mandate."))
            .Concat(parameters
                .Where(parameter => parameter.IsDefined(typeof(BaseConfigValueAttribute)) && config.ContainsKey(parameter.Name ?? string.Empty))
                .Select(parameter => $"Channel <{implementation}>: the parameter '{parameter.Name}' is marked [BaseConfigValue] and can only be set in the base configuration (Pipeline:ChannelConfigs), not in the pipeline definition."))
            .Concat(parameters
                .Where(parameter => IsMandateValue(parameter) && !MandateValueTypes.ContainsKey(parameter.ParameterType))
                .Select(parameter => $"Channel <{implementation}>: the parameter '{parameter.Name}' is marked [MandateValue] but takes {parameter.ParameterType.Name}. A mandate value is a text (string), a list of texts (IReadOnlyList<string>) or a switch (bool)."))
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

                // Without a base configuration, a value nobody else provides may still come from there.
                if (baseConfig is null || ConstructorBinding.TryGetConfiguredValue(parameter, baseConfig, Subject, out _) || ConstructorBinding.IsNullable(parameter))
                    continue;

                errors.Add(IsMandateValue(parameter)
                    ? $"Channel <{implementation}>: the parameter '{parameter.Name}' has no value. Set it in default_config or declare it in mandate_config."
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
    /// Creates the channel for one attempt. The base configuration is read anew every time, so a changed password
    /// applies to reactions that are already waiting.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="config">The values the pipeline definition set: the default configuration of the channel with
    /// the reaction's overwrites.</param>
    /// <param name="mandateValues">The values the mandate supplied; only those for parameters marked as mandate
    /// values are used.</param>
    /// <exception cref="InvalidOperationException">The channel cannot be resolved or a value is missing.</exception>
    public IDeliveryChannel Create(string implementation, Parameterization? config, Parameterization? mandateValues)
    {
        var constructor = GetConstructor(implementation);
        var baseConfig = BaseConfig(implementation);
        var definition = config ?? new Parameterization();
        var mandate = mandateValues ?? new Parameterization();
        var none = new Parameterization();

        // The base configuration comes first: where a channel writes and with which credentials is its decision.
        var arguments = constructor.GetParameters()
            .Select(parameter => Bind(parameter, baseConfig, definition, IsMandateValue(parameter) ? mandate : none))
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

    /// <summary>
    /// Describes the parameters of a channel marked as mandate values: what each one takes and whether the mandate must
    /// supply it. The start ensures they take a text, a list of texts or a switch.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <returns>The type and whether it is required, per parameter name.</returns>
    /// <exception cref="InvalidOperationException">The channel cannot be resolved.</exception>
    internal IReadOnlyDictionary<string, (MandateValueType Type, bool Required)> DescribeMandateValues(string implementation)
        => GetConstructor(implementation)
            .GetParameters()
            .Where(IsMandateValue)
            .ToDictionary(
                parameter => parameter.Name ?? string.Empty,
                parameter => (MandateValueTypes[parameter.ParameterType], !ConstructorBinding.IsNullable(parameter)));

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

    private Parameterization BaseConfig(string implementation)
        => baseConfigs?.GetValueOrDefault(implementation) ?? new Parameterization();
}
