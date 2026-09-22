namespace Geopilot.Api.Services;

/// <summary>
/// Whether a mandate can take a delivery right now, and if not, what stands in the way. The reasons are kept
/// apart because the remedies differ: one is the caller's to fix, the others are the operator's.
/// </summary>
public enum MandateDeliverability
{
    /// <summary>The mandate takes the delivery.</summary>
    Deliverable,

    /// <summary>The mandate does not accept deliveries at all.</summary>
    DeliveryNotAllowed,

    /// <summary>The mandate names no pipeline, or none this installation offers.</summary>
    PipelineNotConfigured,

    /// <summary>The mandate does not accept every file type of the upload.</summary>
    FilesNotAccepted,
}
