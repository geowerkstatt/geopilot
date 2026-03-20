namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Output action for handling output data in a pipeline step.
/// </summary>
public enum OutputAction
{
    /// <summary>
    /// Download the output data.
    /// </summary>
    Download,

    /// <summary>
    /// Deliver the output data.
    /// </summary>
    Delivery,

    /// <summary>
    /// Gets or sets the status message that provides information about the current operation or state.
    /// </summary>
    /// <remarks>This property can be used to display user-friendly messages during long-running operations or
    /// to indicate the result of an action. It is important to ensure that the message is clear and concise to enhance
    /// user experience. The assigned output type has to be a `Dictionary&lt;string, string&gt;` with the localized status message.</remarks>
    StatusMessage,
}
