using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Geopilot.Api.Reactions;

/// <summary>
/// Runs the reactions to deliveries (ADR 0015): claims the due ones from <see cref="ReactionStore"/> one after the
/// other, runs one attempt of each through its channel and reports how it went.
/// </summary>
public class ReactionWorker : BackgroundService
{
    private const string StoppedMessage = "The attempt was interrupted because the application stopped.";
    private const string DeliveryGoneMessage = "The delivery no longer exists.";

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ReactionStore store;
    private readonly DeliveryReactionOptions options;
    private readonly IOptionsMonitor<PipelineOptions> pipelineOptions;
    private readonly string resourcesDirectory;
    private readonly ILogger<ReactionWorker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactionWorker"/> class.
    /// </summary>
    public ReactionWorker(
        IServiceScopeFactory scopeFactory,
        ReactionStore store,
        IOptions<DeliveryReactionOptions> options,
        IOptionsMonitor<PipelineOptions> pipelineOptions,
        IDirectoryProvider directoryProvider,
        ILogger<ReactionWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(directoryProvider);

        this.scopeFactory = scopeFactory;
        this.store = store;
        this.options = options.Value;
        this.pipelineOptions = pipelineOptions;
        this.resourcesDirectory = directoryProvider.ResourcesDirectory;
        this.logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // shortcut: one attempt at a time per instance, so a channel that uses up its time limit holds up the others
        // that long. Run attempts side by side once an installation needs it.
        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            await RunDueReactionsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Runs one attempt of every reaction that is due, until none is left.
    /// </summary>
    internal async Task RunDueReactionsAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await RunNextAsync(stoppingToken))
                    return;
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // An unreachable database must not stop the application; the next poll tries again.
            logger.LogError(ex, "Running the due delivery reactions failed.");
        }
    }

    /// <summary>
    /// Claims the reaction that has been due the longest and runs one attempt of it.
    /// </summary>
    /// <returns>Whether a reaction was due.</returns>
    internal async Task<bool> RunNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();
        var reaction = await store.ClaimNextAsync(context, stoppingToken);
        if (reaction is null)
            return false;

        // The time limit runs from the claim like the lock, so the lock outlasts the attempt by its margin however long
        // the database takes before the channel starts. Kept until the report, so a channel that ignores its token
        // still sees it cancelled.
        using var timeLimit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeLimit.CancelAfter(options.TimeLimit);

        var recorder = scope.ServiceProvider.GetRequiredService<IPipelineRunRecorder>();
        await recorder.RecordReactionAttemptStartedAsync(reaction);
        var result = await AttemptAsync(context, scope.ServiceProvider.GetRequiredService<IAssetFileStore>(), reaction, timeLimit.Token, stoppingToken);

        ReactionState? state;
        try
        {
            // Reported even while the application stops, so an interrupted attempt does not hold its reaction until its
            // lock runs out.
            state = result.Delivered
                ? await store.CompleteAsync(context, reaction, result.Message, CancellationToken.None)
                : await store.FailAsync(context, reaction, result.Message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The reaction stays running until its lock runs out and then runs again or gives up, and its protocol entry
            // keeps the outcome unknown: only this entry tells what the attempt did.
            logger.LogError(ex, "The outcome of attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} could not be saved. Delivered: {Delivered}. {Message}", reaction.Attempts, reaction.ReactionId, reaction.DeliveryId, result.Delivered, result.Message);
            return true;
        }

        await recorder.RecordReactionAttemptFinishedAsync(reaction, state, result.Message);
        LogOutcome(reaction, result.Message, state);
        return true;
    }

    /// <summary>
    /// Runs one attempt of a claimed reaction. Whatever goes wrong becomes a failed attempt with a reason the operators
    /// can read, so the attempt is always reported.
    /// </summary>
    private async Task<ChannelResult> AttemptAsync(Context context, IAssetFileStore assetStore, DeliveryReaction reaction, CancellationToken timeLimit, CancellationToken stoppingToken)
    {
        try
        {
            var delivery = await ReadDeliveryAsync(context, reaction, timeLimit);
            if (delivery is null)
                return ChannelResult.Failure(DeliveryGoneMessage);

            if (!TryCreateChannel(reaction, out var channel, out var refusal))
                return ChannelResult.Failure(refusal);

            var files = delivery.FileNames
                .Select(name => (IDeliveryFile)new AssetFile(assetStore, delivery.Info.JobId, name))
                .ToList();

            // WaitAsync gives up on a channel that does not honour its token.
            // shortcut: such a channel keeps running in the background after its attempt has failed.
            return await channel.DeliverAsync(delivery.Info, files, timeLimit).WaitAsync(timeLimit);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return ChannelResult.Failure(StoppedMessage);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            return ChannelResult.Failure($"The attempt exceeded its time limit of {options.TimeLimit}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} failed unexpectedly.", reaction.Attempts, reaction.ReactionId, reaction.DeliveryId);

            // Only the type: the text of an exception can name internal paths or hosts, and the message is kept for the
            // retention of the protocol and shown to administrators.
            return ChannelResult.Failure($"The attempt failed unexpectedly ({ex.GetType().Name}); the log has the details.");
        }
    }

    /// <summary>
    /// Creates the channel of a reaction from its snapshot and the base configuration as it is now, so a changed base
    /// directory or password applies to reactions that are already waiting.
    /// </summary>
    private bool TryCreateChannel(DeliveryReaction reaction, [NotNullWhen(true)] out IDeliveryChannel? channel, [NotNullWhen(false)] out string? refusal)
    {
        try
        {
            channel = new DeliveryChannelFactory(pipelineOptions.CurrentValue.ChannelConfigs, resourcesDirectory)
                .Create(reaction.Implementation, reaction.Config.Config, reaction.Config.Mandate.ToParameterization());
            refusal = null;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            refusal = ex.Message;
        }
        catch (TargetInvocationException ex)
        {
            // The constructor of the channel refused a value, such as a subfolder outside the base directory.
            refusal = (ex.InnerException ?? ex).Message;
        }

        channel = null;
        return false;
    }

    /// <summary>
    /// Reads what the channel learns about the delivery, anew for every attempt, so the personal data of the declarer
    /// never goes into the reaction.
    /// </summary>
    private static async Task<DeliveryToForward?> ReadDeliveryAsync(Context context, DeliveryReaction reaction, CancellationToken cancellationToken)
    {
        var delivery = await context.Deliveries
            .AsNoTracking()
            .Where(d => d.Id == reaction.DeliveryId)
            .Select(d => new
            {
                d.Id,
                d.JobId,
                d.Date,
                d.Partial,
                PrecursorDeliveryId = EF.Property<int?>(d, "PrecursorDeliveryId"),
                d.Comment,
                MandateId = d.Mandate.Id,
                MandateName = d.Mandate.Name,
                MandateKey = d.Mandate.Key,
                UserName = d.DeclaringUser == null ? null : d.DeclaringUser.FullName,
                UserEmail = d.DeclaringUser == null ? null : d.DeclaringUser.Email,
                ClientName = d.DeclaringClient == null ? null : d.DeclaringClient.Name,
                FileNames = d.Assets.OrderBy(a => a.Id).Select(a => a.SanitizedFilename).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (delivery is null)
            return null;

        var info = new DeliveryInfo
        {
            Id = delivery.Id,
            JobId = delivery.JobId,
            ReactionId = reaction.ReactionId,
            DeclaredAt = new DateTimeOffset(DateTime.SpecifyKind(delivery.Date, DateTimeKind.Utc)),
            Partial = delivery.Partial,
            PrecursorDeliveryId = delivery.PrecursorDeliveryId,
            Comment = string.IsNullOrEmpty(delivery.Comment) ? null : delivery.Comment,
            Mandate = new MandateInfo { Id = delivery.MandateId, Name = delivery.MandateName, Key = delivery.MandateKey },
            Declarer = delivery.UserName is not null
                ? new DeclarerInfo { Kind = DeclarerKind.User, Name = delivery.UserName, Email = string.IsNullOrEmpty(delivery.UserEmail) ? null : delivery.UserEmail }
                : new DeclarerInfo { Kind = DeclarerKind.MachineClient, Name = delivery.ClientName ?? string.Empty },
        };
        return new DeliveryToForward(info, delivery.FileNames);
    }

    private void LogOutcome(DeliveryReaction reaction, string message, ReactionState? state)
    {
        switch (state)
        {
            case ReactionState.Done:
                logger.LogInformation("The delivery reaction {ReactionId} of delivery {DeliveryId} is done: {Message}", reaction.ReactionId, reaction.DeliveryId, message);
                break;
            case ReactionState.Pending:
                logger.LogWarning(
                    "Attempt {Attempt} of the delivery reaction {ReactionId} of delivery {DeliveryId} failed, the next one starts {Wait} after it started: {Message}",
                    reaction.Attempts,
                    reaction.ReactionId,
                    reaction.DeliveryId,
                    options.EffectiveRetryDelays[reaction.Attempts - 1],
                    message);
                break;
            case ReactionState.Error when reaction.Kind == ReactionKind.Transfer:
                logger.LogError("The transfer {ReactionId} of delivery {DeliveryId} gave up after {Attempts} attempts: {Message}", reaction.ReactionId, reaction.DeliveryId, reaction.Attempts, message);
                break;
            case ReactionState.Error:
                // A notification that gave up leaves the state of the delivery as it is.
                logger.LogWarning("The notification {ReactionId} of delivery {DeliveryId} gave up after {Attempts} attempts: {Message}", reaction.ReactionId, reaction.DeliveryId, reaction.Attempts, message);
                break;
            default:
                logger.LogWarning("The delivery reaction {ReactionId} of delivery {DeliveryId} no longer belonged to its attempt when the attempt reported: {Message}", reaction.ReactionId, reaction.DeliveryId, message);
                break;
        }
    }

    private sealed record DeliveryToForward(DeliveryInfo Info, IReadOnlyList<string> FileNames);

    /// <summary>
    /// A delivered file in the asset directory of its job, which the channel can only read.
    /// </summary>
    private sealed class AssetFile : IDeliveryFile
    {
        private readonly IAssetFileStore assetStore;
        private readonly Guid jobId;

        public AssetFile(IAssetFileStore assetStore, Guid jobId, string name)
        {
            this.assetStore = assetStore;
            this.jobId = jobId;
            Name = name;
        }

        public string Name { get; }

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken) => Task.FromResult(assetStore.OpenFile(jobId, Name));
    }
}
