using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.ValidationAttributes;
using Geopilot.PipelineCore.Pipeline;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Id == processName);
    }

    /// <summary>
    /// Resolves a file passed between steps back to its origin by unwrapping <see cref="CopyOnWriteFile"/>.
    /// For an unchanged file, this yields the exact instance created by the pipeline (e.g. an uploaded file
    /// or a step-produced file).
    /// A file that was mutated in place is treated as a new file produced by a step. That is required, not
    /// just a convention: the delivery archives the file through the same copy-on-write view (OpenReadAsync
    /// resolves to Current), so resolving the origin past a materialized copy would label mutated bytes as
    /// the submitted original.
    /// </summary>
    internal static IPipelineFile UnwrapOrigin(this IPipelineFile file)
    {
        var current = file;
        while (current is CopyOnWriteFile copyOnWrite)
        {
            current = copyOnWrite.Current;
        }

        return current;
    }

    /// <summary>
    /// Validates a pipeline definition against its validation attributes. The base configuration of the
    /// processes (appsettings <c>Pipeline:ProcessConfigs</c>) is part of the input because the definition
    /// alone does not decide validity: a key the base configuration pins must not be set by the
    /// definition. Passing none skips that rule, which is what a caller without a hosting layer wants.
    /// </summary>
    internal static PipelineValidationErrors Validate(
        this PipelineProcessConfig pipelineProcessConfig,
        IReadOnlyDictionary<string, Parameterization>? processBaseConfigs = null)
    {
        PipelineValidationErrors validationResults = new PipelineValidationErrors();
        var validationItems = new Dictionary<object, object?>
        {
            [NoBaseConfigOverwriteAttribute.BaseConfigsKey] = processBaseConfigs,
        };

        ValidateRecursive(pipelineProcessConfig, validationResults, validationItems);

        return validationResults;
    }

    private static bool ValidateRecursive(object obj, PipelineValidationErrors errors, IDictionary<object, object?> validationItems)
    {
        bool isValid = true;
        if (obj == null)
            return isValid;
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(obj, serviceProvider: null, validationItems);
        isValid &= Validator.TryValidateObject(obj, context, validationResults, true);
        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                errors.Add(new PipelineValidationError(obj.GetType(), validationResult.MemberNames, validationResult.ErrorMessage ?? "unknown error"));
            }
        }

        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            // Types to not validate any deeper
            var isPrimitive = property.PropertyType.IsPrimitive;
            var isDecimal = property.PropertyType == typeof(decimal);
            var isString = property.PropertyType == typeof(string);
            var isParameterization = property.PropertyType == typeof(Parameterization);
            var isStepInput = property.PropertyType == typeof(InputConfig);
            var isLocalizedText = property.PropertyType == typeof(LocalizedText);

            var goRecursive = !isPrimitive && !isDecimal && !isString && !isParameterization && !isStepInput && !isLocalizedText;
            if (goRecursive)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    if (value is IEnumerable<object> enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (!ValidateRecursive(item, errors, validationItems))
                            {
                                isValid = false;
                            }
                        }
                    }
                    else
                    {
                        if (!ValidateRecursive(value, errors, validationItems))
                        {
                            isValid = false;
                        }
                    }
                }
            }
        }

        return isValid;
    }

    internal static Dictionary<string, object?> ToExpressionParameters(this PipelineContext pipelineContext)
    {
        return pipelineContext.ToExpressionParameters(null, null);
    }

    internal static Dictionary<string, object?> ToExpressionParameters(
        this PipelineContext pipelineContext,
        string? stepId,
        StepResult? stepResult)
    {
        var expressionParameters = new Dictionary<string, object?>();

        foreach (var stepResultKeyValuePair in pipelineContext.StepResults)
        {
            var currentStepId = stepResultKeyValuePair.Key;
            var currentStepResult = stepResultKeyValuePair.Value;
            if (!string.IsNullOrEmpty(currentStepId) && currentStepResult != null)
            {
                expressionParameters = AppendExpressionParameters(expressionParameters, currentStepId, currentStepResult.Result);
            }
        }

        if (stepId != null && stepResult != null)
        {
            expressionParameters = AppendExpressionParameters(expressionParameters, stepId, stepResult.Result);
        }

        return expressionParameters;
    }

    private static Dictionary<string, object?> AppendExpressionParameters(
        Dictionary<string, object?> src,
        string stepId,
        object? result)
    {
        var cpy = new Dictionary<string, object?>(src);
        if (result is null)
            return cpy;

        foreach (var property in result.GetType().GetProperties())
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                cpy[ToParameterKey(stepId, property.Name)] = property.GetValue(result);
            }
        }

        return cpy;
    }

    private static string ToParameterKey(string stepId, string resultKey)
    {
        return $"{stepId}.{resultKey}";
    }
}
