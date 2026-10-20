using Geopilot.Pipeline.Config;
using System.Reflection;

namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Resolves the channel of a reaction and binds its single public constructor by the same rules as a process:
/// parameters marked <see cref="MandateValueAttribute"/> take the values of the mandate, every other parameter the
/// values of the host (<c>Pipeline:ChannelConfigs:&lt;implementation&gt;</c>).
/// </summary>
internal sealed class DeliveryChannelFactory
{
    private const string Subject = "Channel";

    private readonly IReadOnlyDictionary<string, Parameterization>? hostConfigs;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryChannelFactory"/> class.
    /// </summary>
    /// <param name="hostConfigs">The host values per channel implementation, or <see langword="null"/> without a
    /// host layer, which leaves those values unchecked.</param>
    public DeliveryChannelFactory(IReadOnlyDictionary<string, Parameterization>? hostConfigs)
    {
        this.hostConfigs = hostConfigs;
    }

    /// <summary>
    /// Checks a reaction without constructing its channel: the implementation is a channel with one public
    /// constructor, every declared mandate key is a parameter marked as mandate value, every such parameter that
    /// needs a value is declared, and, with a host layer, every other parameter gets a host value it takes.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="mandateKeys">The keys the set declares in <c>mandate_config</c>.</param>
    /// <returns>One message per problem, empty when the reaction is valid.</returns>
    public IReadOnlyList<string> Validate(string implementation, IReadOnlyCollection<string> mandateKeys)
    {
        ArgumentNullException.ThrowIfNull(mandateKeys);

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
        var mandateParameters = parameters.Where(IsMandateValue).Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        var errors = mandateKeys
            .Where(key => !mandateParameters.Contains(key))
            .Select(key => $"Channel <{implementation}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue].")
            .ToList();

        var host = hostConfigs is null ? null : HostConfig(implementation);
        foreach (var parameter in parameters)
        {
            if (IsMandateValue(parameter))
            {
                if (!mandateKeys.Contains(parameter.Name ?? string.Empty) && !ConstructorBinding.IsNullable(parameter))
                    errors.Add($"Channel <{implementation}>: the parameter '{parameter.Name}' takes a mandate value, so the set has to declare it in mandate_config.");

                if (host?.ContainsKey(parameter.Name ?? string.Empty) == true)
                    errors.Add($"Channel <{implementation}>: the parameter '{parameter.Name}' takes a mandate value and cannot be set in Pipeline:ChannelConfigs.");
            }
            else if (host is not null)
            {
                try
                {
                    if (!ConstructorBinding.TryGetConfiguredValue(parameter, host, Subject, out _) && !ConstructorBinding.IsNullable(parameter))
                        errors.Add(MissingValueMessage(parameter));
                }
                catch (InvalidOperationException ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Creates the channel for one attempt. The host values are read anew every time, so a changed password applies
    /// to reactions that are already waiting.
    /// </summary>
    /// <param name="implementation">The fully qualified type name of the channel.</param>
    /// <param name="mandateValues">The values the mandate supplied; only those for parameters marked as mandate
    /// values are used.</param>
    /// <exception cref="InvalidOperationException">The channel cannot be resolved or a value is missing.</exception>
    public IDeliveryChannel Create(string implementation, Parameterization mandateValues)
    {
        ArgumentNullException.ThrowIfNull(mandateValues);

        var constructor = GetConstructor(implementation);
        var host = HostConfig(implementation);
        var arguments = constructor.GetParameters()
            .Select(parameter => Bind(parameter, IsMandateValue(parameter) ? mandateValues : host))
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

    private static bool IsMandateValue(ParameterInfo parameter) => parameter.IsDefined(typeof(MandateValueAttribute));

    private static object? Bind(ParameterInfo parameter, Parameterization values)
    {
        if (ConstructorBinding.TryGetConfiguredValue(parameter, values, Subject, out var value))
            return value;

        if (ConstructorBinding.IsNullable(parameter))
            return null;

        throw new InvalidOperationException(MissingValueMessage(parameter));
    }

    private static string MissingValueMessage(ParameterInfo parameter)
        => $"Channel initialization: No suitable parameter found for parameter of type <{parameter.ParameterType.Name}> and name <{parameter.Name}>. Parameter is not nullable, cannot initialize channel.";

    private Parameterization HostConfig(string implementation)
        => hostConfigs?.GetValueOrDefault(implementation) ?? new Parameterization();
}
