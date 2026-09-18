using Geopilot.Api.Models;

namespace Geopilot.Api.Services;

/// <summary>
/// Checks the delivery fields against the evaluation the mandate configures for each of them. Shared by the
/// declaration itself and by the machine delivery resource, which applies the same rules up front so a caller
/// learns about a missing field before it uploads anything.
/// </summary>
public static class DeliveryFieldValidator
{
    /// <summary>
    /// Validates the fields for the mandate.
    /// </summary>
    /// <param name="mandate">The mandate whose evaluation rules apply.</param>
    /// <param name="fields">The fields the caller supplied.</param>
    /// <param name="precursorDelivery">The delivery <see cref="DeliveryFields.PrecursorDeliveryId"/> resolves to,
    /// or <see langword="null"/> if it resolves to none.</param>
    /// <returns>The violated rules keyed by field name, empty when all fields are acceptable.</returns>
    public static Dictionary<string, string[]> Validate(Mandate mandate, DeliveryFields fields, Delivery? precursorDelivery)
    {
        ArgumentNullException.ThrowIfNull(mandate);
        ArgumentNullException.ThrowIfNull(fields);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = new List<string>();
                errors[field] = messages;
            }

            messages.Add(message);
        }

        if (mandate.EvaluatePrecursorDelivery == FieldEvaluationType.NotEvaluated && fields.PrecursorDeliveryId.HasValue)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery is not allowed for this mandate.");
        }
        else if (mandate.EvaluatePrecursorDelivery == FieldEvaluationType.Required && !fields.PrecursorDeliveryId.HasValue)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery is required for this mandate.");
        }

        if (fields.PrecursorDeliveryId.HasValue && precursorDelivery is null)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery not found.");
        }

        if (mandate.EvaluatePartial == FieldEvaluationType.NotEvaluated && fields.PartialDelivery.HasValue)
        {
            AddError(nameof(fields.PartialDelivery), "Partial delivery is not allowed for this mandate.");
        }
        else if (mandate.EvaluatePartial == FieldEvaluationType.Required && !fields.PartialDelivery.HasValue)
        {
            AddError(nameof(fields.PartialDelivery), "Partial delivery is required for this mandate.");
        }

        if (mandate.EvaluateComment == FieldEvaluationType.NotEvaluated && !string.IsNullOrWhiteSpace(fields.Comment))
        {
            AddError(nameof(fields.Comment), "Comment is not allowed for this mandate.");
        }
        else if (mandate.EvaluateComment == FieldEvaluationType.Required && string.IsNullOrWhiteSpace(fields.Comment))
        {
            AddError(nameof(fields.Comment), "Comment is required for this mandate.");
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
    }
}
