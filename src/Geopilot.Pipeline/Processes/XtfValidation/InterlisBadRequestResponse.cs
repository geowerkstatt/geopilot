using System;
using System.Collections.Generic;
using System.Text;

namespace Geopilot.Pipeline.Processes.XtfValidation;

/// <summary>
/// Result of interlis-check-service for malformed requests (400 Bad Request).
/// </summary>
/// <remarks>The actual return type is Microsoft.AspNetCore.Mvc.ValidationProblemDetails. This is a stub.</remarks>
public class InterlisBadRequestResponse
{
    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    public string? Detail { get; set; }
}
