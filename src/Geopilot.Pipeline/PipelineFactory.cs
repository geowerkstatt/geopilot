using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
public class PipelineFactory : IPipelineFactory
{
    // Same naming convention and LocalizedText handling as the deserializer in Builder.Yaml, so the
    // snapshot reads like the definition it was taken from; JsonCompatible because the snapshot is
    // stored and queried as JSON.
    private static readonly ISerializer SnapshotSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new LocalizedTextYamlConverter())
        .JsonCompatible()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly string pipelineTempDirectory;
    private readonly string? resourcesDirectory;
    private readonly IReadOnlyDictionary<string, Parameterization>? processBaseConfigs;
    private readonly IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs;

    /// <summary>
    /// The pipeline process configuration used to create pipelines.
    /// </summary>
    internal PipelineProcessConfig PipelineProcessConfig { get; }

    private IPipelineProcessFactory pipelineProcessFactory;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IPipelineProcessFactory pipelineProcessFactory,
        string pipelineTempDirectory,
        string? resourcesDirectory,
        IReadOnlyDictionary<string, Parameterization>? processBaseConfigs,
        IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs,
        ILoggerFactory loggerFactory)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pipeline process configuration.");
        this.pipelineProcessFactory = pipelineProcessFactory;
        this.pipelineTempDirectory = pipelineTempDirectory;
        this.resourcesDirectory = resourcesDirectory;
        this.processBaseConfigs = processBaseConfigs;
        this.channelBaseConfigs = channelBaseConfigs;

        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<PipelineFactory>();
    }

    /// <inheritdoc />
    public List<PipelineConfig> Pipelines => PipelineProcessConfig.Pipelines;

    /// <inheritdoc />
    public IReadOnlyList<ReactionSetConfig> ReactionSets => PipelineProcessConfig.ReactionSets ?? [];

    /// <inheritdoc />
    public IReadOnlyList<(string ReactionId, string Implementation, Parameterization Config)> ResolveTransfers(string reactionSetId)
        => Resolve(FindReactionSet(reactionSetId).Transfers ?? []);

    /// <inheritdoc />
    public IReadOnlyList<(string ReactionId, string Implementation, Parameterization Config)> ResolveNotifications(string reactionSetId)
        => Resolve(FindReactionSet(reactionSetId).Notifications ?? []);

    /// <inheritdoc />
    public IReadOnlyList<MandateField> DescribeMandateFields(string reactionSetId)
    {
        var set = FindReactionSet(reactionSetId);
        var implementations = Resolve(set.AllReactions()).ToDictionary(reaction => reaction.ReactionId, reaction => reaction.Implementation);
        var channelFactory = new DeliveryChannelFactory(channelBaseConfigs, resourcesDirectory);

        return set.AllReactions()
            .Where(reaction => reaction.MandateConfig is { Count: > 0 })
            .SelectMany(reaction =>
            {
                var values = channelFactory.DescribeMandateValues(implementations[reaction.Id]);
                return (reaction.MandateConfig ?? []).Select(entry => new MandateField(reaction.Id, entry.Key, entry.DisplayName, values[entry.Key].Type, values[entry.Key].Required));
            })
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ValidateMandateValues(string reactionSetId, IReadOnlyDictionary<string, Parameterization> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var channelFactory = new DeliveryChannelFactory(channelBaseConfigs, resourcesDirectory);
        var errors = new List<string>();
        foreach (var reaction in Resolve(FindReactionSet(reactionSetId).AllReactions()))
        {
            try
            {
                channelFactory.Create(reaction.Implementation, reaction.Config, values.GetValueOrDefault(reaction.ReactionId));
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"reaction {reaction.ReactionId}: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                // The constructor of the channel refused a value, such as a subfolder outside the base directory.
                errors.Add($"reaction {reaction.ReactionId}: {(ex.InnerException ?? ex).Message}");
            }
        }

        return errors;
    }

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id, Guid jobId)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        var jobPipelineDirectory = Path.Combine(pipelineTempDirectory, jobId.ToString());

        if (pipelineConfig != null)
        {
            return Geopilot.Pipeline.Pipeline.Builder()
                .Id(pipelineConfig.Id)
                .DisplayName(pipelineConfig.DisplayName)
                .Steps(CreateSteps(pipelineConfig, jobPipelineDirectory, jobId))
                .Logger(PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger<Geopilot.Pipeline.Pipeline>())
                    .PipelineId(id)
                    .JobId(jobId)
                    .Build())
                .PipelineDirectory(jobPipelineDirectory)
                .JobId(jobId)
                .Build();
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    /// <inheritdoc />
    public string GetDefinitionSnapshotJson(string pipelineId)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == pipelineId)
            ?? throw new InvalidOperationException($"pipeline for '{pipelineId}' not found");

        var referencedProcessIds = pipelineConfig.Steps
            .Select(step => step.ProcessId)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var processes = PipelineProcessConfig.Processes
            .Where(process => referencedProcessIds.Contains(process.Id))
            .ToList();

        var implementations = processes.Select(process => process.Implementation).ToHashSet(StringComparer.Ordinal);
        var processConfigs = processBaseConfigs?
            .Where(entry => implementations.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        var snapshot = new DefinitionSnapshot
        {
            Pipelines = new List<PipelineConfig> { pipelineConfig },
            Processes = processes,
            ProcessConfigs = processConfigs is { Count: > 0 } ? processConfigs : null,
        };

        return SnapshotSerializer.Serialize(snapshot);
    }

    /// <inheritdoc />
    public PipelineDefinitionValidationResult ValidateDefinition()
    {
        var definitionErrors = PipelineProcessConfig.Validate(processBaseConfigs, channelBaseConfigs);
        if (definitionErrors.HasErrors)
        {
            return new PipelineDefinitionValidationResult(
                $"errors in pipeline definition:{Environment.NewLine}{definitionErrors.ErrorMessage}");
        }

        var processErrors = new HashSet<string>();
        var processes = PipelineProcessConfig.Processes;
        foreach (var pipeline in PipelineProcessConfig.Pipelines)
        {
            var stepResultTypes = pipelineProcessFactory.BuildStepResultTypes(pipeline.Steps, processes);
            foreach (var step in pipeline.Steps)
            {
                try
                {
                    // Use Validate() rather than Build() so validation does not actually construct processes.
                    // Invoking constructors here would leak per-step directories under $TEMP and allocate
                    // HttpClients and other per-process resources on every restart.
                    pipelineProcessFactory
                        .Builder()
                        .PipelineId(pipeline.Id)
                        .StepConfig(step)
                        .StepResultTypes(stepResultTypes)
                        .Processes(processes)
                        .Validate(resourcesDirectory);
                }
                catch (Exception ex)
                {
                    processErrors.Add($"pipeline {pipeline.Id}, step {step.Id}, process {step.ProcessId}, error: {ex.Message}");
                }
            }
        }

        var reactionErrors = ValidateReactionSets();
        if (processErrors.Count > 0 || reactionErrors.Count > 0)
        {
            var sections = new List<string>();
            if (processErrors.Count > 0)
                sections.Add($"Invalid pipeline processes found:{Environment.NewLine}{string.Join(Environment.NewLine, processErrors)}");
            if (reactionErrors.Count > 0)
                sections.Add($"Invalid reaction sets found:{Environment.NewLine}{string.Join(Environment.NewLine, reactionErrors)}");

            return new PipelineDefinitionValidationResult(string.Join(Environment.NewLine, sections));
        }

        return PipelineDefinitionValidationResult.Valid;
    }

    /// <summary>
    /// Checks the channel of every reaction against its default configuration with the reaction's overwrites, the
    /// declared mandate values and, with a base configuration, the base values, without constructing a channel.
    /// </summary>
    private List<string> ValidateReactionSets()
    {
        var channelFactory = new DeliveryChannelFactory(channelBaseConfigs, resourcesDirectory);
        var channels = PipelineProcessConfig.Channels ?? [];
        return (PipelineProcessConfig.ReactionSets ?? [])
            .SelectMany(set => set.AllReactions().SelectMany(reaction =>
            {
                // The definition validation has already rejected a reaction without a defined channel.
                var channel = channels.Single(candidate => candidate.Id == reaction.ChannelId);
                return channelFactory
                    .Validate(channel.Implementation, MergeChannelConfig(channel, reaction), reaction.MandateConfig?.Select(entry => entry.Key).ToList() ?? [])
                    .Select(error => $"reaction set {set.Id}, reaction {reaction.Id}, channel {channel.Id}, error: {error}");
            }))
            .ToList();
    }

    private ReactionSetConfig FindReactionSet(string reactionSetId)
        => ReactionSets.FirstOrDefault(candidate => candidate.Id == reactionSetId)
            ?? throw new InvalidOperationException($"Reaction set <{reactionSetId}> does not exist.");

    // Per reaction its id, the implementation of its channel and the configuration the definition gives it.
    private List<(string ReactionId, string Implementation, Parameterization Config)> Resolve(IEnumerable<ReactionConfig> reactions)
    {
        var channels = PipelineProcessConfig.Channels ?? [];
        return reactions
            .Select(reaction =>
            {
                var channel = channels.Single(candidate => candidate.Id == reaction.ChannelId);
                return (reaction.Id, channel.Implementation, MergeChannelConfig(channel, reaction));
            })
            .ToList();
    }

    private static Parameterization MergeChannelConfig(ChannelConfig channel, ReactionConfig reaction)
    {
        var config = new Parameterization(channel.DefaultConfig ?? new Parameterization());
        foreach (var overwrite in reaction.ChannelConfigOverwrites ?? new Parameterization())
        {
            config[overwrite.Key] = overwrite.Value;
        }

        return config;
    }

    private List<IPipelineStep> CreateSteps(PipelineConfig pipelineConfig, string pipelineTempDirectory, Guid jobId)
    {
        return pipelineConfig.Steps
            .Select(s => CreateStep(s, pipelineConfig.Id, pipelineTempDirectory, jobId) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig, string pipelineId, string pipelineTempDirectory, Guid jobId)
    {
        return PipelineStep.Builder()
            .Id(stepConfig.Id)
            .DisplayName(stepConfig.DisplayName)
            .Inputs(InputCompiler.Compile(stepConfig.Input ?? new InputConfig()))
            .OutputActions(stepConfig.OutputActions ?? new List<OutputActionConfig>())
            .StepConditions(stepConfig.Conditions)
            .PipelineDirectory(pipelineTempDirectory)
            .ResourcesDirectory(resourcesDirectory)
            .Process(pipelineProcessFactory.Builder()
                .PipelineId(pipelineId)
                .StepConfig(stepConfig)
                .Processes(PipelineProcessConfig.Processes)
                .PipelineDirectory(pipelineTempDirectory)
                .ResourcesDirectory(resourcesDirectory)
                .JobId(jobId)
                .Build())
            .Logger(PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger<PipelineStep>())
                    .StepId(stepConfig.Id)
                    .PipelineId(pipelineId)
                    .JobId(jobId)
                    .Build())
            .Build();
    }

    /// <summary>
    /// Creates a builder for a <see cref="PipelineFactory"/>. This is the entry point for creating
    /// pipelines outside the host, for example from a plugin's integration tests.
    /// </summary>
    /// <returns>A new <see cref="PipelineFactoryBuilder"/>.</returns>
    public static PipelineFactoryBuilder Builder() => new PipelineFactoryBuilder();

    /// <summary>
    /// Fluent builder for a <see cref="PipelineFactory"/>. The pipeline definition, the process factory,
    /// the logger factory and the pipeline working directory are required; the resources root and the
    /// process base configuration are optional.
    /// </summary>
    public class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IPipelineProcessFactory? pipelineProcessFactory;
        private string? pipelineTempDirectory;
        private string? resourcesDirectory;
        private IReadOnlyDictionary<string, Parameterization>? processBaseConfigs;
        private IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs;
        private ILoggerFactory? loggerFactory;

        /// <summary>
        /// Supplies the pipeline definition as YAML text.
        /// </summary>
        /// <param name="processDefinition">The YAML pipeline definition.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder Yaml(string processDefinition)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new LocalizedTextYamlConverter())
                .Build();
            this.pipelineProcessConfig = deserializer.Deserialize<PipelineProcessConfig>(processDefinition);
            return this;
        }

        /// <summary>
        /// Supplies the pipeline definition by reading it from a YAML file.
        /// </summary>
        /// <param name="path">Path to the YAML pipeline definition.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder File(string path)
        {
            var yaml = System.IO.File.ReadAllText(path);
            return Yaml(yaml);
        }

        /// <summary>
        /// Supplies the process factory used to instantiate the process of each step.
        /// </summary>
        /// <param name="pipelineProcessFactory">The process factory.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder PipelineProcessFactory(IPipelineProcessFactory pipelineProcessFactory)
        {
            this.pipelineProcessFactory = pipelineProcessFactory;
            return this;
        }

        /// <summary>
        /// Supplies the logger factory used for pipeline and step logging.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Supplies the root directory under which the per-job pipeline working directories are created.
        /// </summary>
        /// <param name="pipelineTempDirectory">The pipeline working directory root.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder PipelineTempDirectory(string pipelineTempDirectory)
        {
            this.pipelineTempDirectory = pipelineTempDirectory;
            return this;
        }

        /// <summary>
        /// Supplies the root directory that <c>${file(path)}</c> references resolve against.
        /// </summary>
        /// <param name="resourcesDirectory">The resources root, or <see langword="null"/> when the definition uses no file references.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder ResourcesDirectory(string? resourcesDirectory)
        {
            this.resourcesDirectory = resourcesDirectory;
            return this;
        }

        /// <summary>
        /// Supplies the base configuration of the processes as the hosting layer defines it (appsettings
        /// <c>Pipeline:ProcessConfigs</c>), keyed by process implementation. That layer cannot be overridden,
        /// so <see cref="ValidateDefinition"/> rejects a definition setting one of its keys. Omitting it skips
        /// that rule, which is what a caller without a hosting layer wants.
        /// </summary>
        /// <param name="processBaseConfigs">The base configuration per process implementation, or <see langword="null"/> when there is no hosting layer.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder ProcessBaseConfigs(IReadOnlyDictionary<string, Parameterization>? processBaseConfigs)
        {
            this.processBaseConfigs = processBaseConfigs;
            return this;
        }

        /// <summary>
        /// Supplies the base configuration of the delivery channels (appsettings <c>Pipeline:ChannelConfigs</c>),
        /// keyed by channel implementation. That layer cannot be overridden, so <see cref="ValidateDefinition"/>
        /// rejects a definition setting one of its keys, and it also checks that every channel gets the base values
        /// it needs. Omitting it skips both, which is what a caller without a hosting layer wants.
        /// </summary>
        /// <param name="channelBaseConfigs">The base configuration per channel implementation, or <see langword="null"/> when there is no hosting layer.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder ChannelBaseConfigs(IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs)
        {
            this.channelBaseConfigs = channelBaseConfigs;
            return this;
        }

        /// <summary>
        /// Builds the configured <see cref="PipelineFactory"/>.
        /// </summary>
        /// <returns>The configured factory.</returns>
        /// <exception cref="InvalidOperationException">A required part was not supplied.</exception>
        public PipelineFactory Build()
        {
            if (this.pipelineProcessFactory == null)
                throw new InvalidOperationException("Pipeline process factory is required but was not provided.");
            if (this.loggerFactory == null)
                throw new InvalidOperationException("Logger factory is required but was not provided.");
            if (this.pipelineTempDirectory == null)
                throw new InvalidOperationException("Pipeline temp directory is required but was not provided.");

            return new PipelineFactory(pipelineProcessConfig, pipelineProcessFactory, pipelineTempDirectory, resourcesDirectory, processBaseConfigs, channelBaseConfigs, loggerFactory);
        }
    }
}
