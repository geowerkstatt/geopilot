using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

public class StepConditionConfig
{
    [YamlMember(Alias = "from")]
    public required string From { get; set; }

    [YamlMember(Alias = "take")]
    public required string Take { get; set; }

    [YamlMember(Alias = "matches")]
    public required string Matches { get; set; }

    [YamlMember(Alias = "action")]
    public required HashSet<StepConditionAction>? Action { get; set; }
}
