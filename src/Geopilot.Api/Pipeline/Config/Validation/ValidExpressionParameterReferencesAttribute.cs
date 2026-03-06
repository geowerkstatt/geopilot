using Itenso.TimePeriod;
using NCalc;
using NCalc.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ValidExpressionParameterReferencesAttribute : ValidationAttribute
{
    private static char parameterSeparator = '.';
    private static string parameterPattern = "^(\\w+)[" + parameterSeparator + "](\\w+)$";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineConfig pipeline)
        {
            return new ValidationResult("validation object is not of type PipelineConfig");
        }

        var allSteps = pipeline.Steps ?? new List<StepConfig>();

        var errorMessages = new List<string>();
        if (allSteps != null)
        {
            var stepConditionsErrorMessges = allSteps
                .SelectMany(s => GetStepConditionErrorMessages(s, pipeline))
                .ToList();
            errorMessages.AddRange(stepConditionsErrorMessges);
        }

        errorMessages.AddRange(GetExpressionErrorMessages(pipeline.DeliveryCondition, pipeline, null, "Pipeline-Delivery-Condition"));

        if (errorMessages.Count > 0)
        {
            return new ValidationResult(string.Join(Environment.NewLine, errorMessages));
        }
        else
        {
            return ValidationResult.Success;
        }
    }

    private List<string> GetStepConditionErrorMessages(
        StepConfig stepToValidate,
        PipelineConfig pipeline)
    {
        var errorMessages = new List<string>();

        errorMessages.AddRange(GetExpressionErrorMessages(stepToValidate.Conditions?.Pre?.SkipCondition, pipeline, stepToValidate, "Step-Pre-Skip-Condition"));
        errorMessages.AddRange(GetExpressionErrorMessages(stepToValidate.Conditions?.Pre?.FailCondition, pipeline, stepToValidate, "Step-Pre-Fail-Condition"));
        errorMessages.AddRange(GetExpressionErrorMessages(stepToValidate.Conditions?.Post?.FailCondition, pipeline, null, "Step-Post-Fail-Condition"));

        return errorMessages;
    }

    private IEnumerable<string> GetExpressionErrorMessages(
        string? expression,
        PipelineConfig pipeline,
        StepConfig? currentStep,
        string field)
    {
        if (!string.IsNullOrEmpty(expression))
        {
            var mathematicalExpression = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache);
            try
            {
                var evaluateAsync = mathematicalExpression.EvaluateAsync();
            }
            catch (NCalcException e)
            {
                if (currentStep != null)
                    return new List<string>() { $"pipeline '{pipeline.Id}', step '{currentStep.Id}', invalid expression '{expression}' on field {field}: {e.Message}" };
                else
                    return new List<string>() { $"pipeline '{pipeline.Id}, invalid expression '{expression}' on field {field}: {e.Message}" };
            }

            return mathematicalExpression.GetParameterNames()
                .Where(p => !ValidParameterName(p, currentStep, pipeline.Parameters, pipeline.Steps))
                .Select(p =>
                {
                    if (currentStep != null)
                        return $"pipeline '{pipeline.Id}', step '{currentStep.Id}', invalid expression '{expression}' on field {field}, parameter '{p}' is not valid";
                    else
                        return $"pipeline '{pipeline.Id}', invalid expression '{expression}' on field {field}, parameter '{p}' is not valid";
                });
        }

        return new List<string>();
    }

    private static bool ValidParameterName(string parameterName, StepConfig? currentStep, PipelineParametersConfig pipelineParameters, List<StepConfig> allSteps)
    {
        if (string.IsNullOrEmpty(parameterName))
            return false;

        if (parameterName == "null")
            return true;

        if (!Regex.IsMatch(parameterName, parameterPattern))
            return false;

        var parameterParts = parameterName.Split(parameterSeparator);
        var stepId = parameterParts[0];
        var resultId = parameterParts[1];

        var isValidStepOutputReference = IsValidStepOutputReference(stepId, resultId, currentStep?.Id, allSteps);
        var isValidPipelineParamReference = IsValidPipelineParamReference(stepId, resultId, pipelineParameters);

        return isValidStepOutputReference || isValidPipelineParamReference;
    }

    private static bool IsValidStepOutputReference(string stepId, string take, string? currentStepId, List<StepConfig> allSteps)
    {
        if (allSteps != null)
        {
            return allSteps
                .TakeWhile(s => currentStepId != null ? s.Id != currentStepId : true)
                .Any(s => s.Id == stepId && HasOutput(s.Output, take));
        }
        else
        {
            return false;
        }
    }

    private static bool HasOutput(List<OutputConfig>? outputConfig, string take)
    {
        if (outputConfig != null)
        {
            return outputConfig.Any(o => o.As == take);
        }
        else
        {
            // we return true as the step has a required annotation on the output
            // and we don't want to fail twice on the same issue (missing output reference)
            return true;
        }
    }

    private static bool IsValidPipelineParamReference(string stepId, string attribute, PipelineParametersConfig pipelineParameters)
    {
        if (pipelineParameters != null &&
            pipelineParameters.Mappings != null &&
            pipelineParameters.UploadStep == stepId &&
            pipelineParameters.Mappings.Any(m => m.Attribute == attribute))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
