using Geopilot.Api.Pipeline.Config;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;

namespace Geopilot.Api.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Id == processName);
    }

    internal static PipelineValidationErrors Validate(this PipelineProcessConfig pipelineProcessConfig, PipelineValidationErrors? inputErrors = null)
    {
        PipelineValidationErrors validationResults = new PipelineValidationErrors();
        var isValid = ValidateReqursive(pipelineProcessConfig, validationResults);

        return validationResults;
    }

    private static bool ValidateReqursive(object obj, PipelineValidationErrors errors)
    {
        bool isValid = true;
        if (obj == null)
            return isValid;
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(obj);
        isValid = Validator.TryValidateObject(obj, context, validationResults, true);
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
                            if (!ValidateReqursive(item, errors))
                            {
                                isValid = false;
                            }
                        }
                    }
                    else
                    {
                        if (!ValidateReqursive(value, errors))
                        {
                            isValid = false;
                        }
                    }
                }
            }
        }

        return isValid;
    }
}
