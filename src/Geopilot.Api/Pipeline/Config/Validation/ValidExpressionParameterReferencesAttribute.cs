using Microsoft.Extensions.Logging.Abstractions;
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

        if (pipeline.DeliveryRestrictions != null)
        {
            foreach (var condition in pipeline.DeliveryRestrictions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, null, "Pipeline-Delivery-Restriction"));
        }

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

        if (stepToValidate.Conditions?.Pre?.SkipConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Pre.SkipConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, "Step-Pre-Skip-Condition"));
        }

        if (stepToValidate.Conditions?.Pre?.FailConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Pre.FailConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, "Step-Pre-Fail-Condition"));
        }

        if (stepToValidate.Conditions?.Post?.FailConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Post.FailConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, null, "Step-Post-Fail-Condition"));
        }

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
            var runner = ConditionEvaluator.CreateRunner(expression, new NullLogger<ValidExpressionParameterReferencesAttribute>());
            List<string> parameterNames;
            try
            {
                // if the expression is invalid, we will get an exception here and can return it as an error message
                parameterNames = runner.GetParameterNames();
            }
            catch (NCalcException e)
            {
                if (currentStep != null)
                    return new List<string>() { $"pipeline '{pipeline.Id}', step '{currentStep.Id}', invalid expression '{expression}' on field {field}: {e.Message}" };
                else
                    return new List<string>() { $"pipeline '{pipeline.Id}, invalid expression '{expression}' on field {field}: {e.Message}" };
            }

            return parameterNames
                .Where(p => !ValidParameterName(p, currentStep, pipeline.Steps))
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

    private static bool ValidParameterName(string parameterName, StepConfig? currentStep, List<StepConfig> allSteps)
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

        return IsValidStepOutputReference(stepId, resultId, currentStep?.Id, allSteps);
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
}
