using Geopilot.PipelineCore.Pipeline;
using System;
using System.Collections.Generic;
using System.Text;

namespace Geopilot.Pipeline.Processes.ZipPackage;

internal class ZipPackageResult
{
    public IPipelineFile? ZipPackage { get; set; }

    public required LocalizedText StatusMessage { get; set; }
}
