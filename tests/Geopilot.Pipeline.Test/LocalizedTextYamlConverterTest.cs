using Geopilot.PipelineCore.Pipeline;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class LocalizedTextYamlConverterTest
{
    private sealed class Holder
    {
        public LocalizedText? DisplayName { get; set; }
    }

    [TestMethod]
    public void DeserializesMappingIntoLocalizedText()
    {
        var yaml = "display_name:\n  de: Validierung\n  en: Validation\n";
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new LocalizedTextYamlConverter())
            .Build();

        var holder = deserializer.Deserialize<Holder>(yaml);

        LocalizedText expected = new Dictionary<string, string> { { "de", "Validierung" }, { "en", "Validation" } };
        Assert.AreEqual(expected, holder.DisplayName);
    }
}
