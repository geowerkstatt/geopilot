using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NCalc.Exceptions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Geopilot.Pipeline.Process;

/// <summary>
/// Provides functionality to create and initialize pipeline process instances based on configuration settings and
/// registered plugins.
/// </summary>
/// <remarks>The PipelineProcessFactory is responsible for resolving and instantiating process implementations
/// specified in pipeline configuration. It supports both built-in and plugin-based process types, allowing for
/// extensibility via external assemblies. This factory is typically used in scenarios where pipeline steps are
/// dynamically configured and require runtime resolution of their processing logic. Thread safety is not guaranteed; if
/// used concurrently, external synchronization is required.</remarks>
public class PipelineProcessFactory : IPipelineProcessFactory, IDisposable
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PipelineProcessFactory> logger;
    private readonly PipelineOptions pipelineOptions;
    private readonly GrpcChannel ilitoolsWrapperChannel;

    private HashSet<Assembly> processorPluginAssemblies = new HashSet<Assembly>();
    private HashSet<AssemblyLoadContext> processorPluginLoadContexts = new HashSet<AssemblyLoadContext>();
    private bool disposed;

    /// <summary>
    /// Disposes the resources used by the PipelineProcessFactory.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources and unloads assemblies.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                foreach (var processorPluginLoadContext in processorPluginLoadContexts)
                {
                    processorPluginLoadContext.Unload();
                }

                processorPluginLoadContexts.Clear();
                processorPluginAssemblies.Clear();
                ilitoolsWrapperChannel.Dispose();
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Initializes a new instance of the PipelineProcessFactory class using the specified configuration settings.
    /// </summary>
    /// <remarks>The constructor loads plugin assemblies specified in the "Pipeline:Plugins" section of the
    /// configuration. Assemblies are loaded into a dedicated context, allowing for isolation and dynamic plugin
    /// management. If no plugins are configured, the factory will operate without any loaded assemblies.</remarks>
    /// <param name="pipelinePluginOptions">Pipeline plugin options containing configuration settings. Cannot be null.</param>
    /// <param name="ilitoolsOptions">Ilitools options containing configuration settings. Cannot be null.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers for process instances. Cannot be null.</param>
    public PipelineProcessFactory(IOptions<PipelineOptions> pipelinePluginOptions, IOptions<IlitoolsOptions> ilitoolsOptions, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(pipelinePluginOptions);
        ArgumentNullException.ThrowIfNull(ilitoolsOptions);

        this.loggerFactory = loggerFactory;
        this.pipelineOptions = pipelinePluginOptions.Value;
        this.logger = loggerFactory.CreateLogger<PipelineProcessFactory>();

        ilitoolsWrapperChannel = GrpcChannel.ForAddress(ilitoolsOptions.Value.IlitoolsWrapperAddress);
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        if (pipelineOptions.Plugins == null)
        {
            logger.LogInformation("No processor plugins configured. Not loading any plugins.");
            return;
        }

        var loadContextLogger = loggerFactory.CreateLogger<ProcessPluginLoadContext>();

        foreach (var assemblyPath in pipelineOptions.Plugins)
        {
            var assemblyFullPath = Path.IsPathRooted(assemblyPath) ? assemblyPath : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, assemblyPath));

            var assemblyContext = ProcessPluginLoadContext.Create(assemblyFullPath, loadContextLogger);
            if (assemblyContext == null)
            {
                continue;
            }

            var plugin = assemblyContext.LoadFromAssemblyPath(assemblyFullPath);

            processorPluginAssemblies.Add(plugin);
            processorPluginLoadContexts.Add(assemblyContext);

            logger.LogInformation($"Plugin loaded: {assemblyPath}");
        }
    }

    /// <inheritdoc />
    public IPipelineProcessBuilder Builder()
    {
        return new PipelineProcessBuilder(this, processorPluginAssemblies, loggerFactory, pipelineOptions, ilitoolsWrapperChannel);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Type> BuildStepResultTypes(List<StepConfig> steps, List<ProcessConfig> processes)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(processes);

        var stepResultTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (step.ProcessId is null)
                continue;

            var processConfig = processes.GetProcessConfig(step.ProcessId);
            if (processConfig is null)
                continue;

            var processType = GetProcessorType(processConfig.Implementation);
            if (processType is null)
                continue;

            var resultType = ProcessReflection.ResolveResultType(processType);
            if (resultType is not null)
                stepResultTypes[step.Id] = resultType;
        }

        return stepResultTypes;
    }

    /// <summary>
    /// Resolves a process implementation type name to its <see cref="Type"/>: built-in processes (namespace
    /// <c>Geopilot.Pipeline.Processes</c>) are looked up across the loaded app domain, everything else in the
    /// registered plugin assemblies. Returns <see langword="null"/> when the type cannot be resolved.
    /// </summary>
    internal Type? GetProcessorType(string implementation)
    {
        if (implementation.StartsWith("Geopilot.Pipeline.Processes", StringComparison.Ordinal))
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(implementation))
                .FirstOrDefault(t => t != null);
        }
        else if (this.processorPluginAssemblies.Count > 0)
        {
            foreach (var assembly in this.processorPluginAssemblies)
            {
                var type = assembly.GetType(implementation);
                if (type != null)
                {
                    return type;
                }
            }
        }

        logger.LogWarning($"For process implementation '{implementation}' no processor plugin configured. Cannot load process.");
        return null;
    }

    internal class PipelineProcessBuilder : IPipelineProcessBuilder
    {
        private readonly PipelineProcessFactory factory;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        private readonly HashSet<Assembly> processorPluginAssemblies = new HashSet<Assembly>();
        private readonly PipelineOptions pipelineOptions;
        private readonly GrpcChannel ilitoolsWrapperChannel;

        private string? pipelineId;
        private StepConfig? stepConfig;
        private List<ProcessConfig>? processes;
        private IReadOnlyDictionary<string, Type>? stepResultTypes;
        private string? pipelineDirectory;
        private Guid jobId;

        /// <summary>
        /// Initializes a new instance of the PipelineProcessBuilder class with the specified plugin assemblies, load
        /// contexts, logger factory, and pipeline options.
        /// </summary>
        /// <remarks>Use this constructor to configure the PipelineProcessBuilder with all required
        /// dependencies for plugin-based pipeline processing. Supplying appropriate assemblies and load contexts allows
        /// for flexible plugin management and versioning.</remarks>
        /// <param name="factory">The factory that created this builder; used to resolve process types.</param>
        /// <param name="processorPluginAssemblies">A set of assemblies that contain processor plugins to be included in the pipeline.</param>
        /// <param name="loggerFactory">The factory used to create loggers for pipeline processing operations.</param>
        /// <param name="pipelineOptions">The options that configure the behavior and execution parameters of the pipeline.</param>
        /// <param name="ilitoolsWrapperChannel">The gRPC channel used for communication with the ilitools-wrapper service.</param>
        public PipelineProcessBuilder(
            PipelineProcessFactory factory,
            HashSet<Assembly> processorPluginAssemblies,
            ILoggerFactory loggerFactory,
            PipelineOptions pipelineOptions,
            GrpcChannel ilitoolsWrapperChannel)
        {
            this.factory = factory;
            this.processorPluginAssemblies = processorPluginAssemblies;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<PipelineProcessBuilder>();
            this.pipelineOptions = pipelineOptions;
            this.ilitoolsWrapperChannel = ilitoolsWrapperChannel;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder PipelineId(string pipelineId)
        {
            this.pipelineId = pipelineId;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder StepConfig(StepConfig stepConfig)
        {
            this.stepConfig = stepConfig;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder Processes(List<ProcessConfig> processes)
        {
            this.processes = processes;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder StepResultTypes(IReadOnlyDictionary<string, Type> stepResultTypes)
        {
            this.stepResultTypes = stepResultTypes;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder PipelineDirectory(string pipelineDirectory)
        {
            this.pipelineDirectory = pipelineDirectory;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder JobId(Guid jobId)
        {
            this.jobId = jobId;
            return this;
        }

        /// <summary>
        /// Validates a step's output actions against its process result type at load time: each
        /// <c>output_actions.property</c> must be a readable property of the result type, and each action
        /// must be compatible with that property's type. Returns one message per problem; empty when valid
        /// or when a single result type cannot be resolved.
        /// </summary>
        private static List<string> ValidateOutputActions(Type processType, List<OutputActionConfig>? outputActions)
        {
            if (outputActions is null || outputActions.Count == 0)
                return new List<string>();

            var errors = new List<string>();

            var resultType = ProcessReflection.ResolveResultType(processType);
            if (resultType is null)
                return errors;

            foreach (var outputAction in outputActions)
            {
                if (outputAction.Actions is null)
                    continue;

                var property = resultType.GetProperty(outputAction.Property);

                if (property is null || !property.CanRead)
                {
                    errors.Add($"output action references property '{outputAction.Property}', which is not a readable property of the result type <{resultType.Name}> of process <{processType.Name}>.");
                    continue;
                }

                foreach (var action in outputAction.Actions)
                {
                    if (!IsTypeCompatible(action, property.PropertyType))
                        errors.Add($"output action property '{outputAction.Property}' of process <{processType.Name}> cannot be used with action {action}: its type <{property.PropertyType.Name}> is not compatible.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Whether a result property of the given type may be tagged with <paramref name="action"/>:
        /// <c>Download</c>/<c>Delivery</c> require an <c>IPipelineFile</c> or a collection of them,
        /// <c>StatusMessage</c> a <c>LocalizedText</c> or a string-to-string dictionary (<c>IReadOnlyDictionary</c>
        /// or <c>IDictionary</c>, kept for backward compatibility to mirror what
        /// <c>PipelineStep.NormalizeStatusMessage</c> accepts at run time), and <c>Visualization</c> an
        /// <c>IVisualization</c>. Actions without a type rule are accepted.
        /// </summary>
        private static bool IsTypeCompatible(OutputAction action, Type propertyType)
        {
            bool Is<T>() => typeof(T).IsAssignableFrom(propertyType);

            if (action is OutputAction.Download or OutputAction.Delivery)
                return Is<IPipelineFile>() || Is<IEnumerable<IPipelineFile>>();
            if (action == OutputAction.StatusMessage)
                return Is<LocalizedText>() || Is<IReadOnlyDictionary<string, string>>() || Is<IDictionary<string, string>>();
            if (action == OutputAction.Visualization)
                return Is<IVisualization>();

            return true;
        }

        /// <summary>
        /// Validates a step's condition expressions against the result types of the steps they reference at
        /// load time: each <c>stepId.property</c> parameter must name a readable property of that step's result
        /// type. Expression syntax and reference scope are already enforced by
        /// <c>ValidExpressionParameterReferencesAttribute</c> in an earlier pass, so this only adds the property
        /// check. References to steps whose result type cannot be resolved are left unchecked rather than
        /// reported as errors, matching the input reference validator.
        /// </summary>
        private static List<string> ValidateConditions(StepConfig stepConfig, IReadOnlyDictionary<string, Type> stepResultTypes)
        {
            var errors = new List<string>();

            foreach (var expression in CollectConditionExpressions(stepConfig))
            {
                var runner = ConditionEvaluator.CreateRunner(expression, NullLogger.Instance);

                List<string> parameterNames;
                try
                {
                    parameterNames = runner.GetParameterNames();
                }
                catch (NCalcException ex)
                {
                    errors.Add($"condition '{expression}' is not a valid expression: {ex.Message}");
                    continue;
                }

                foreach (var parameterName in parameterNames)
                {
                    if (!TryGetStepProperty(parameterName, out var stepId, out var propertyName))
                        continue;

                    if (!stepResultTypes.TryGetValue(stepId, out var resultType))
                        continue;

                    var property = resultType.GetProperty(propertyName);
                    if (property is null || !property.CanRead)
                        errors.Add($"condition '{expression}' references property '{propertyName}', which is not a readable property of the result type <{resultType.Name}> of step <{stepId}>.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Yields every non-empty condition expression of a step across all condition kinds
        /// (pre skip/fail and post fail/warn/restrict-delivery).
        /// </summary>
        private static IEnumerable<string> CollectConditionExpressions(StepConfig stepConfig)
        {
            var conditions = stepConfig.Conditions;
            if (conditions is null)
                yield break;

            var conditionLists = new[]
            {
                conditions.Pre?.SkipConditions,
                conditions.Pre?.FailConditions,
                conditions.Post?.FailConditions,
                conditions.Post?.WarnConditions,
                conditions.Post?.RestrictDeliveryConditions,
            };

            foreach (var conditionList in conditionLists)
            {
                if (conditionList is null)
                    continue;

                foreach (var condition in conditionList)
                {
                    if (!string.IsNullOrEmpty(condition.Expression))
                        yield return condition.Expression;
                }
            }
        }

        /// <summary>
        /// Splits an NCalc parameter name of the form <c>stepId.property</c> into its two parts. Returns
        /// <see langword="false"/> for anything else (e.g. the <c>null</c> literal), so such parameters are
        /// skipped rather than treated as a step-output reference.
        /// </summary>
        private static bool TryGetStepProperty(string parameterName, out string stepId, out string propertyName)
        {
            stepId = string.Empty;
            propertyName = string.Empty;

            var parts = parameterName.Split('.');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                return false;

            stepId = parts[0];
            propertyName = parts[1];
            return true;
        }

        /// <inheritdoc />
        public object Build()
        {
            ArgumentNullException.ThrowIfNull(pipelineDirectory);

            var (objectType, constructor, processParameterization) = PrepareProcessDescriptor();
            var parameters = constructor.GetParameters()
                .Select(p => GenerateParameter(p, objectType, processParameterization, pipelineDirectory, jobId))
                .ToArray();
            var processInstance = Activator.CreateInstance(objectType, parameters);
            if (processInstance != null)
                return processInstance;

            var processId = stepConfig != null ? stepConfig.ProcessId : string.Empty;
            throw new InvalidOperationException($"Failed to create process instance for step <{stepConfig?.Id}> with process ID <{processId}> and implementation <{objectType.FullName}>.");
        }

        /// <inheritdoc />
        public void Validate(string? resourcesRoot = null)
        {
            ArgumentNullException.ThrowIfNull(stepConfig);

            var (objectType, constructor, processParameterization) = PrepareProcessDescriptor();
            foreach (var parameterInfo in constructor.GetParameters())
            {
                ValidateParameter(parameterInfo, processParameterization);
            }

            var stepResultTypes = this.stepResultTypes ?? new Dictionary<string, Type>();
            var validationErrors = InputBindingValidator.Validate(objectType, stepConfig.Input, resourcesRoot, stepResultTypes)
                .Concat(ValidateOutputActions(objectType, stepConfig.OutputActions))
                .Concat(ValidateConditions(stepConfig, stepResultTypes))
                .ToList();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, validationErrors));
            }
        }

        /// <summary>
        /// Resolves the process type, constructor, and merged parameterization for the
        /// currently configured step. Shared by <see cref="Build"/> and <see cref="Validate"/>
        /// so both paths surface the same errors at the same points; neither path has side
        /// effects here (no instance creation, no directory creation).
        /// </summary>
        private (Type ProcessType, ConstructorInfo Constructor, Parameterization Parameterization) PrepareProcessDescriptor()
        {
            ArgumentNullException.ThrowIfNull(stepConfig);
            ArgumentNullException.ThrowIfNull(processes);

            var processConfig = stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;

            if (processConfig == null)
                throw new InvalidOperationException($"No process config found for process ID <{stepConfig.ProcessId}>.");

            var objectType = factory.GetProcessorType(processConfig.Implementation) ?? throw new InvalidOperationException($"Process <{processConfig.Implementation}> is unknown");

            var constructors = objectType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length != 1)
                throw new InvalidOperationException($"Process <{processConfig.Implementation}> has {constructors.Length} public constructors. A Process must have exactly one public constructor.");

            var constructor = constructors[0];
            var processBaseConfig = pipelineOptions.ProcessConfigs.GetValueOrDefault(processConfig.Implementation);
            var processParameterization = GetMergedParameterization(processBaseConfig, processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);
            return (objectType, constructor, processParameterization);
        }

        /// <summary>
        /// Mirror of <see cref="GenerateParameter"/> without the side-effectful materialization
        /// step (no <see cref="PipelineLogger"/> / <see cref="PipelineFileManager"/> allocation,
        /// no value conversion kept). Throws with the same message <see cref="GenerateParameter"/>
        /// would for an unsatisfiable non-nullable parameter.
        /// </summary>
        private static void ValidateParameter(ParameterInfo parameterInfo, Parameterization processConfig)
        {
            // Framework-provided dependencies are always satisfiable by Build (logger, file
            // manager, optional container runner). Skipping them here is how we avoid invoking
            // their constructors at startup.
            if (parameterInfo.ParameterType == typeof(ILogger) ||
                parameterInfo.ParameterType == typeof(IPipelineFileManager) ||
                parameterInfo.ParameterType == typeof(IIli2GpkgClient))
            {
                return;
            }

            if (TryGetConfiguredValue(parameterInfo, processConfig, out _))
                return;

            if (IsParameterNullable(parameterInfo))
                return;

            throw new InvalidOperationException($"Process initialization: No suitable parameter found for parameter of type <{parameterInfo.ParameterType.Name}> and name <{parameterInfo.Name}>. Parameter is not nullable, cannot initialize process.");
        }

        /// <summary>
        /// Resolves the configured value for a constructor parameter. Returns false when the
        /// parameter is not configured. A configured value that cannot be converted to the
        /// parameter type is a configuration error and throws, even for a nullable parameter:
        /// silently falling back to null would hide typos in the pipeline definition. A
        /// configured null counts as convertible only for nullable parameter types.
        /// </summary>
        private static bool TryGetConfiguredValue(ParameterInfo parameterInfo, Parameterization processConfig, out object? convertedValue)
        {
            convertedValue = null;
            if (string.IsNullOrEmpty(parameterInfo.Name) || !processConfig.TryGetValue(parameterInfo.Name, out var rawValue))
                return false;

            if (RawValueConverter.TryConvert(rawValue, parameterInfo.ParameterType, out convertedValue))
                return true;

            if (rawValue is not null)
                throw new InvalidOperationException($"Process initialization: The configured value <{FormatRawValue(rawValue)}> for parameter <{parameterInfo.Name}> cannot be converted to type <{FormatTypeName(parameterInfo.ParameterType)}>.");

            return false;
        }

        private static string FormatRawValue(object rawValue)
        {
            try
            {
                return JsonSerializer.Serialize(rawValue);
            }
            catch (Exception)
            {
                return rawValue.ToString() ?? string.Empty;
            }
        }

        private static string FormatTypeName(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
                return FormatTypeName(underlyingType) + "?";

            if (!type.IsGenericType)
                return type.Name;

            var genericName = type.Name[..type.Name.IndexOf('`')];
            return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(FormatTypeName))}>";
        }

        private object? GenerateParameter(ParameterInfo parameterInfo, Type processType, Parameterization processConfig, string pipelineDirectory, Guid jobId)
        {
            if (parameterInfo.ParameterType == typeof(ILogger))
            {
                return PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger(processType))
                    .PipelineId(pipelineId ?? string.Empty)
                    .StepId(stepConfig?.Id ?? string.Empty)
                    .JobId(jobId)
                    .Build();
            }
            else if (parameterInfo.ParameterType == typeof(IPipelineFileManager))
            {
                return new PipelineFileManager(pipelineDirectory, this.stepConfig?.Id ?? throw new InvalidOperationException("Step Id must be provided."));
            }
            else if (parameterInfo.ParameterType == typeof(IIli2GpkgClient))
            {
                return new Ili2GpkgClient(ilitoolsWrapperChannel, loggerFactory.CreateLogger<Ili2GpkgClient>());
            }
            else if (TryGetConfiguredValue(parameterInfo, processConfig, out var convertedValue))
            {
                return convertedValue;
            }

            if (IsParameterNullable(parameterInfo))
                return null;
            else
                throw new InvalidOperationException($"Process initialization: No suitable parameter found for parameter of type <{parameterInfo.ParameterType.Name}> and name <{parameterInfo.Name}>. Parameter is not nullable, cannot initialize process.");
        }

        private static bool IsParameterNullable(ParameterInfo parameterInfo)
        {
            return new NullabilityInfoContext().Create(parameterInfo).WriteState is NullabilityState.Nullable;
        }

        private Parameterization GetMergedParameterization(Parameterization? processBaseConfig, Parameterization? processDefaultConfig, Parameterization? processDefaultConfigOverwrites)
        {
            var mergedParams = new Parameterization();

            // Start with processBaseConfig (lowest priority)
            if (processBaseConfig != null)
            {
                foreach (var config in processBaseConfig)
                {
                    mergedParams[config.Key] = config.Value;
                }
            }

            // Merge processDefaultConfig (medium priority)
            if (processDefaultConfig != null)
            {
                foreach (var config in processDefaultConfig)
                {
                    if (processBaseConfig != null && processBaseConfig.ContainsKey(config.Key))
                        throw new InvalidOperationException($"Conflict in process configuration: The key '{config.Key}' is defined in both process base configuration and process default configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten.");
                    else
                        mergedParams[config.Key] = config.Value;
                }
            }

            // Apply processDefaultConfigOverwrites (highest priority)
            if (processDefaultConfigOverwrites != null)
            {
                foreach (var overwrite in processDefaultConfigOverwrites)
                {
                    if (processBaseConfig != null && processBaseConfig.ContainsKey(overwrite.Key))
                        throw new InvalidOperationException($"Conflict in process configuration overwrite: The key '{overwrite.Key}' is defined in both process base configuration and process overwrite configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten.");
                    if (processDefaultConfig == null || !processDefaultConfig.ContainsKey(overwrite.Key))
                        throw new InvalidOperationException($"Conflict in process configuration overwrite: The key '{overwrite.Key}' is not defined in process default configuration, so it cannot be overwritten. Please ensure that only existing default configuration keys are overwritten.");
                    mergedParams[overwrite.Key] = overwrite.Value;
                }
            }

            return mergedParams;
        }
    }
}
