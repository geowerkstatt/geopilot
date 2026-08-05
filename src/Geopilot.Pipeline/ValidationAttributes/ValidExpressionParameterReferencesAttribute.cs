using Geopilot.Pipeline.Config;
using Microsoft.Extensions.Logging.Abstractions;
using NCalc.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.ValidationAttributes;

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

        // PRE-conditions may reference only earlier steps (includeCurrentStep: false); POST-conditions run
        // after the step, so they may also reference the current step's own output (includeCurrentStep: true).
        if (stepToValidate.Conditions?.Pre?.SkipConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Pre.SkipConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, includeCurrentStep: false, "Step-Pre-Skip-Condition"));
        }

        if (stepToValidate.Conditions?.Pre?.FailConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Pre.FailConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, includeCurrentStep: false, "Step-Pre-Fail-Condition"));
        }

        if (stepToValidate.Conditions?.Post?.FailConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Post.FailConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, includeCurrentStep: true, "Step-Post-Fail-Condition"));
        }

        if (stepToValidate.Conditions?.Post?.WarnConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Post.WarnConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, includeCurrentStep: true, "Step-Post-Warn-Condition"));
        }

        if (stepToValidate.Conditions?.Post?.RestrictDeliveryConditions != null)
        {
            foreach (var condition in stepToValidate.Conditions.Post.RestrictDeliveryConditions)
                errorMessages.AddRange(GetExpressionErrorMessages(condition.Expression, pipeline, stepToValidate, includeCurrentStep: true, "Step-Post-Restrict-Delivery-Condition"));
        }

        return errorMessages;
    }

    private IEnumerable<string> GetExpressionErrorMessages(
        string? expression,
        PipelineConfig pipeline,
        StepConfig currentStep,
        bool includeCurrentStep,
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
                return new List<string>() { $"pipeline '{pipeline.Id}', step '{currentStep.Id}', invalid expression '{expression}' on field {field}: {e.Message}" };
            }

            return parameterNames
                .Where(p => !ValidParameterName(p, currentStep, includeCurrentStep, pipeline.Steps))
                .Select(p => $"pipeline '{pipeline.Id}', step '{currentStep.Id}', invalid expression '{expression}' on field {field}, parameter '{p}' is not valid");
        }

        return new List<string>();
    }

    private static bool ValidParameterName(string parameterName, StepConfig currentStep, bool includeCurrentStep, List<StepConfig> allSteps)
    {
        if (string.IsNullOrEmpty(parameterName))
            return false;

        if (parameterName == "null")
            return true;

        if (!Regex.IsMatch(parameterName, parameterPattern))
            return false;

        var stepId = parameterName.Split(parameterSeparator)[0];

        return IsValidStepOutputReference(stepId, currentStep.Id, includeCurrentStep, allSteps);
    }

    // Steps before the current one are always referenceable; the current step itself only when
    // includeCurrentStep is true (POST-conditions, where its own output already exists). Later steps
    // are never referenceable, since their outputs do not exist yet when the condition is evaluated.
    private static bool IsValidStepOutputReference(string stepId, string? currentStepId, bool includeCurrentStep, List<StepConfig> allSteps)
    {
        if (stepId == currentStepId)
        {
            return includeCurrentStep;
        }

        foreach (var step in allSteps)
        {
            if (step.Id == stepId)
            {
                return true;
            }

            if (step.Id == currentStepId)
            {
                break;
            }
        }

        return false;
    }
}
