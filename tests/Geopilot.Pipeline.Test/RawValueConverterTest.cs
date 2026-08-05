using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class RawValueConverterTest
{
    private enum TestEnum
    {
        FirstValue,
        SecondValue,
    }

    [TestMethod]
    [DataRow("FirstValue")]
    [DataRow("firstValue")]
    [DataRow("FIRSTVALUE")]
    public void ConvertsEnumFromStringIgnoringCase(string rawName)
    {
        Assert.IsTrue(RawValueConverter.TryConvert(rawName, typeof(TestEnum), out var converted));
        Assert.AreEqual(TestEnum.FirstValue, converted);
    }

    [TestMethod]
    [DataRow("SecondValue")]
    [DataRow("secondValue")]
    [DataRow("SECONDVALUE")]
    public void ConvertsEnumListFromStringListIgnoringCase(string rawName)
    {
        var raw = new List<object> { "firstValue", rawName };

        Assert.IsTrue(RawValueConverter.TryConvert(raw, typeof(IReadOnlyList<TestEnum>), out var converted));
        var list = (IReadOnlyList<TestEnum>)converted!;
        CollectionAssert.AreEqual(new[] { TestEnum.FirstValue, TestEnum.SecondValue }, list.ToArray());
    }

    [TestMethod]
    public void RejectsEnumListWithUnknownName()
    {
        var raw = new List<object> { "firstValue", "no such value" };

        Assert.IsFalse(RawValueConverter.TryConvert(raw, typeof(IReadOnlyList<TestEnum>), out var converted));
        Assert.IsNull(converted);
    }

    [TestMethod]
    public void ConvertsStringSetFromStringList()
    {
        var raw = new List<object> { "map", "tree" };

        Assert.IsTrue(RawValueConverter.TryConvert(raw, typeof(HashSet<string>), out var converted));
        var set = (HashSet<string>)converted!;
        Assert.HasCount(2, set);
        Assert.Contains("map", set);
        Assert.Contains("tree", set);
    }

    [TestMethod]
    public void ConvertsNullForNullableTargetOnly()
    {
        Assert.IsTrue(RawValueConverter.TryConvert(null, typeof(int?), out var nullableConverted));
        Assert.IsNull(nullableConverted);

        Assert.IsFalse(RawValueConverter.TryConvert(null, typeof(int), out var nonNullableConverted));
        Assert.IsNull(nonNullableConverted);
    }
}
