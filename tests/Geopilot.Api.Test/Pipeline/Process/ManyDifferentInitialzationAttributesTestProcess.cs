using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Api.Test.Pipeline.Process;

public class ManyDifferentInitialzationAttributesTestProcess
{
    public ILogger? Logger { get; set; }

    public string MandatoryString { get; set; }

    public string? OptionalString { get; set; }

    public int MandatoryInt { get; set; }

    public int? OptionalInt { get; set; }

    public double MandatoryDouble { get; set; }

    public double? OptionalDouble { get; set; }

    public bool MandatoryBoolean { get; set; }

    public bool? OptionalBoolean { get; set; }

    [PipelineProcessInitialize]
    public void Initialize(
        string mandatoryString,
        string? optionalString,
        int mandatoryInt,
        int? optionalInt,
        double mandatoryDouble,
        double? optionalDouble,
        bool mandatoryBoolean,
        bool? optionalBoolean,
        ILogger logger)
    {
        this.Logger = logger;
        this.MandatoryString = mandatoryString;
        this.OptionalString = optionalString;
        this.MandatoryInt = mandatoryInt;
        this.OptionalInt = optionalInt;
        this.MandatoryDouble = mandatoryDouble;
        this.OptionalDouble = optionalDouble;
        this.MandatoryBoolean = mandatoryBoolean;
        this.OptionalBoolean = optionalBoolean;
    }
}
