using Geopilot.PipelineCore.Pipeline;
using System;
using System.Collections.Generic;
using System.Text;

namespace Geopilot.Pipeline.Processes.XtfValidation;

internal class XtfValidatorResult
{
    public bool ValidationSuccessful { get; set; }

    public required LocalizedText StatusMessage { get; set; }

    public IPipelineFile? ErrorLog { get; set; }

    public IPipelineFile? XtfLog { get; set; }
}
