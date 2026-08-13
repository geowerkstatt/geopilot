using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents a summary of a mandate.
/// </summary>
/// <param name="Id">The unique identifier of the mandate.</param>
/// <param name="Name">The display name of the mandate.</param>
/// <param name="Description">The description of the mandate.</param>
/// <param name="AllowDelivery">Indicates whether delivery is allowed for this mandate.</param>
/// <param name="EvaluatePrecursorDelivery">Defines how <see cref="Delivery.PrecursorDelivery"/> is evaluated.</param>
/// <param name="EvaluatePartial">Defines how <see cref="Delivery.Partial"/> is evaluated.</param>
/// <param name="EvaluateComment">Defines how <see cref="Delivery.Comment"/> is evaluated.</param>
public record MandateSummary(
    int Id,
    LocalizedText Name,
    LocalizedText Description,
    bool AllowDelivery,
    FieldEvaluationType EvaluatePrecursorDelivery,
    FieldEvaluationType EvaluatePartial,
    FieldEvaluationType EvaluateComment);
