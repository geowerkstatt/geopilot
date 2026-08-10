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

    [TestMethod]
    public void ConvertsStringToInt()
    {
        Assert.IsTrue(RawValueConverter.TryConvert("42", typeof(int), out var converted));
        Assert.AreEqual(42, converted);
    }

    [TestMethod]
    public void ConvertsStringToDouble()
    {
        // "1.5" also guards invariant-culture parsing: on a comma-decimal machine culture it must still parse.
        Assert.IsTrue(RawValueConverter.TryConvert("1.5", typeof(double), out var converted));
        Assert.AreEqual(1.5, converted);
    }

    [TestMethod]
    public void ConvertsStringToBool()
    {
        object expectedTrue = true;
        Assert.IsTrue(RawValueConverter.TryConvert("true", typeof(bool), out var converted));
        Assert.AreEqual(expectedTrue, converted);
    }

    [TestMethod]
    public void ConvertsStringToTimeSpan()
    {
        Assert.IsTrue(RawValueConverter.TryConvert("00:00:01", typeof(TimeSpan), out var converted));
        Assert.AreEqual(TimeSpan.FromSeconds(1), converted);
    }

    [TestMethod]
    public void CanConvertReportsYesForAssignableTargetInEitherDirection()
    {
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(int), typeof(int)));
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(string), typeof(object)));
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(object), typeof(string)));
    }

    [TestMethod]
    public void CanConvertReportsYesForSupportedStringConversion()
    {
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(string), typeof(int)));
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(string), typeof(TimeSpan)));
        Assert.AreEqual(RawValueConverter.Convertibility.Yes, RawValueConverter.CanConvert(typeof(string), typeof(TestEnum)));
    }

    [TestMethod]
    public void CanConvertReportsNoForNonAssignableInterfaceOrAbstractTarget()
    {
        Assert.AreEqual(RawValueConverter.Convertibility.No, RawValueConverter.CanConvert(typeof(int), typeof(IDisposable)));
        Assert.AreEqual(RawValueConverter.Convertibility.No, RawValueConverter.CanConvert(typeof(int), typeof(Array)));
    }

    [TestMethod]
    public void CanConvertReportsMaybeForConcreteNonAssignableTarget()
    {
        Assert.AreEqual(RawValueConverter.Convertibility.Maybe, RawValueConverter.CanConvert(typeof(int), typeof(Uri)));
    }
}
