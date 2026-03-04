using Geopilot.Api.Pipeline.Config;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Geopilot.Api.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Id == processName);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
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
            var goRecursive = !property.PropertyType.IsPrimitive && property.PropertyType != typeof(decimal) && property.PropertyType != typeof(string);
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
                expressionParameters = AppendExpressionParameters(expressionParameters, currentStepId, currentStepResult.Outputs);
            }
        }

        if (stepId != null && stepResult != null)
        {
            expressionParameters = AppendExpressionParameters(expressionParameters, stepId, stepResult.Outputs);
        }

        return expressionParameters;
    }

    private static Dictionary<string, object?> AppendExpressionParameters(
        Dictionary<string, object?> src,
        string stepId,
        Dictionary<string, StepOutput> outputs)
    {
        Dictionary<string, object?> cpy = new Dictionary<string, object?>(src);
        foreach (var output in outputs)
        {
            var resultKey = output.Key;
            var resultValue = output.Value.Data;
            if (!string.IsNullOrEmpty(resultKey))
            {
                cpy[ToParameterKey(stepId, resultKey)] = resultValue;
            }
        }

        return cpy;
    }

    private static string ToParameterKey(string stepId, string resultKey)
    {
        return $"{stepId}.{resultKey}";
    }
}
