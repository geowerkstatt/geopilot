using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Id == processName);
    }

    internal static PipelineValidationErrors Validate(this PipelineProcessConfig pipelineProcessConfig, PipelineValidationErrors? inputErrors = null)
    {
        PipelineValidationErrors validationResults = new PipelineValidationErrors();
        var isValid = ValidateRecursive(pipelineProcessConfig, validationResults);

        return validationResults;
    }

    private static bool ValidateRecursive(object obj, PipelineValidationErrors errors)
    {
        bool isValid = true;
        if (obj == null)
            return isValid;
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(obj);
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
                            if (!ValidateRecursive(item, errors))
                            {
                                isValid = false;
                            }
                        }
                    }
                    else
                    {
                        if (!ValidateRecursive(value, errors))
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
                expressionParameters = AppendExpressionParameters(expressionParameters, currentStepId, currentStepResult);
            }
        }

        if (stepId != null && stepResult != null)
        {
            expressionParameters = AppendExpressionParameters(expressionParameters, stepId, stepResult);
        }

        return expressionParameters;
    }

    private static Dictionary<string, object?> AppendExpressionParameters(
        Dictionary<string, object?> src,
        string stepId,
        StepResult stepResult)
    {
        var cpy = new Dictionary<string, object?>(src);
        var result = stepResult.Result;
        if (result != null)
        {
            foreach (var property in result.GetType().GetProperties())
            {
                if (property.CanRead)
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
