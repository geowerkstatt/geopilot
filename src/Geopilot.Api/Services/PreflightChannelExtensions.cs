using System.Threading.Channels;

namespace Geopilot.Api.Services;

/// <summary>
/// Registers a bounded in-memory channel (capacity 50) for queueing cloud preflight work.
/// When the channel is full, producers wait until space is available.
/// </summary>
public static class PreflightChannelExtensions
{
    /// <summary>
    /// Adds the preflight channel to the service collection.
    /// </summary>
    public static IServiceCollection AddPreflightChannel(this IServiceCollection services)
    {
        var channel = Channel.CreateBounded<PreflightRequest>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        return services;
    }
}
