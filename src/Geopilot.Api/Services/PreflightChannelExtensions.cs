using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Registers an unbounded in-memory channel for queueing cloud preflight work.
/// </summary>
public static class PreflightChannelExtensions
{
    /// <summary>
    /// Adds the preflight channel to the service collection.
    /// </summary>
    public static IServiceCollection AddPreflightChannel(this IServiceCollection services)
    {
        var channel = Channel.CreateUnbounded<PreflightRequest>();
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        return services;
    }
}
