using Geopilot.Pipeline.Channels;
using System.Collections.Concurrent;

namespace Geopilot.Api.Reactions;

/// <summary>
/// A channel for the tests of <see cref="ReactionWorker"/>, which creates it by its type name like any channel. Its
/// <c>probe</c> value names the <see cref="ChannelProbe"/> of one test, so tests running in parallel do not meet.
/// </summary>
internal sealed class ProbeChannel : IDeliveryChannel
{
    private static readonly ConcurrentDictionary<string, ChannelProbe> Probes = new();

    private readonly ChannelProbe probe;

    public ProbeChannel(string probe, string? baseValue, int? port, [MandateValue] string? subfolder)
    {
        this.probe = Probes[probe];
        this.probe.Created(baseValue, port, subfolder);
    }

    /// <summary>
    /// Makes a probe known to the channel and returns the value that names it.
    /// </summary>
    internal static string Register(ChannelProbe probe)
    {
        var name = Guid.NewGuid().ToString();
        Probes[name] = probe;
        return name;
    }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => probe.DeliverAsync(delivery, files, cancellationToken);
}

/// <summary>
/// Decides how the attempts of a <see cref="ProbeChannel"/> go and records what the channel was given.
/// </summary>
internal sealed class ChannelProbe
{
    private readonly Func<CancellationToken, Task<ChannelResult>> deliver;
    private readonly List<string?> baseValues = [];

    public ChannelProbe(Func<CancellationToken, Task<ChannelResult>> deliver)
    {
        this.deliver = deliver;
    }

    /// <summary>
    /// The base value of every channel created for the probe, in order.
    /// </summary>
    public IReadOnlyList<string?> BaseValues => baseValues;

    public int? Port { get; private set; }

    public string? Subfolder { get; private set; }

    public DeliveryInfo? Delivery { get; private set; }

    /// <summary>
    /// The name and the content of every file the channel was given.
    /// </summary>
    public IReadOnlyList<(string Name, string Content)> Files { get; private set; } = [];

    internal void Created(string? baseValue, int? port, string? subfolder)
    {
        baseValues.Add(baseValue);
        Port = port;
        Subfolder = subfolder;
    }

    internal async Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
    {
        Delivery = delivery;
        var read = new List<(string Name, string Content)>();
        foreach (var file in files)
        {
            using var reader = new StreamReader(await file.OpenReadAsync(cancellationToken));
            read.Add((file.Name, await reader.ReadToEndAsync(cancellationToken)));
        }

        Files = read;
        return await deliver(cancellationToken);
    }
}
