using Geopilot.Api.Pipeline.Process.XtfDiff;
using System.Globalization;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class WktCurveLinearizerTest
{
    [TestMethod]
    [DataRow("POINT (2640300 1172560)")]
    [DataRow("LINESTRING (1 2, 3 4)")]
    [DataRow("POLYGON ((1 2, 3 4, 5 6, 1 2))")]
    [DataRow("MULTIPOINT ((1 2), (3 4))")]
    [DataRow("MULTILINESTRING ((1 2, 3 4))")]
    [DataRow("MULTIPOLYGON (((1 2, 3 4, 5 6, 1 2)))")]
    [DataRow("COMPOUNDCURVE EMPTY")]
    public void LinearTypesAndEmptyPassThroughUnchanged(string wkt)
    {
        Assert.AreEqual(wkt, WktCurveLinearizer.Linearize(wkt));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CURVEPOLYGON ((1 2, 3 4")]
    [DataRow("COMPOUNDCURVE (FOO (1 2, 3 4))")]
    [DataRow("CURVEPOLYGON ((1 2, not a number))")]
    public void MalformedInputIsReturnedUnchanged(string wkt)
    {
        Assert.AreEqual(wkt, WktCurveLinearizer.Linearize(wkt));
    }

    [TestMethod]
    public void CompoundCurveWithStraightSegmentsBecomesLineString()
    {
        var linearized = WktCurveLinearizer.Linearize("COMPOUNDCURVE ((1 2, 3 4, 5 6, 7 8))");
        Assert.AreEqual("LINESTRING (1 2, 3 4, 5 6, 7 8)", linearized);
    }

    [TestMethod]
    public void CurvePolygonWithStraightRingBecomesPolygon()
    {
        var linearized = WktCurveLinearizer.Linearize("CURVEPOLYGON ((10 1, 11 5, 19 6, 10 1))");
        Assert.AreEqual("POLYGON ((10 1, 11 5, 19 6, 10 1))", linearized);
    }

    [TestMethod]
    public void CurvePolygonWithCompoundCurveRingBecomesPolygon()
    {
        var linearized = WktCurveLinearizer.Linearize("CURVEPOLYGON (COMPOUNDCURVE ((0 0, 10 0, 10 10, 0 0)))");
        Assert.AreEqual("POLYGON ((0 0, 10 0, 10 10, 0 0))", linearized);
    }

    [TestMethod]
    public void ThirdDimensionIsDropped()
    {
        var linearized = WktCurveLinearizer.Linearize("COMPOUNDCURVE ((1 2 99, 3 4 98))");
        Assert.AreEqual("LINESTRING (1 2, 3 4)", linearized);
    }

    [TestMethod]
    public void CollinearCircularStringBecomesStraightLineString()
    {
        var linearized = WktCurveLinearizer.Linearize("CIRCULARSTRING (0 0, 5 0, 10 0)");
        Assert.AreEqual("LINESTRING (0 0, 5 0, 10 0)", linearized);
    }

    [TestMethod]
    public void CounterClockwiseArcIsStrokedAlongTheCircle()
    {
        // Half circle on the circle with center (5, 0) and radius 5, running through the top (5 5).
        var linearized = WktCurveLinearizer.Linearize("CIRCULARSTRING (0 0, 5 5, 10 0)");
        var points = ParseLineString(linearized);

        Assert.AreEqual((0d, 0d), points[0]);
        Assert.AreEqual((10d, 0d), points[^1]);
        Assert.IsTrue(points.Count > 10, "the arc is stroked into many short segments");
        foreach (var (x, y) in points)
        {
            var radius = Math.Sqrt(((x - 5) * (x - 5)) + (y * y));
            Assert.AreEqual(5, radius, 0.001, $"point ({x} {y}) lies on the circle");
            Assert.IsTrue(y >= 0, "the arc runs through the upper half plane");
        }
    }

    [TestMethod]
    public void ClockwiseArcIsStrokedThroughTheLowerHalfPlane()
    {
        // Same circle as above, but the mid point (5 -5) forces the clockwise direction.
        var linearized = WktCurveLinearizer.Linearize("CIRCULARSTRING (0 0, 5 -5, 10 0)");
        var points = ParseLineString(linearized);

        Assert.AreEqual((0d, 0d), points[0]);
        Assert.AreEqual((10d, 0d), points[^1]);
        Assert.IsTrue(points.Any(p => p.Y < -4), "the arc runs through the lower half plane");
        Assert.IsTrue(points.All(p => p.Y <= 0), "no point lies in the upper half plane");
    }

    [TestMethod]
    public void MultiArcCircularStringStrokesEveryArc()
    {
        // Two arcs: quarter circle up to (5 5) on circle center (5 0), then continuing to (10 10).
        var linearized = WktCurveLinearizer.Linearize("CIRCULARSTRING (0 0, 5 5, 10 0, 15 -5, 20 0)");
        var points = ParseLineString(linearized);

        Assert.AreEqual((0d, 0d), points[0]);
        Assert.AreEqual((20d, 0d), points[^1]);
        Assert.IsTrue(points.Any(p => p.Y > 4), "first arc passes the upper half plane");
        Assert.IsTrue(points.Any(p => p.Y < -4), "second arc passes the lower half plane");
    }

    [TestMethod]
    public void CompoundCurveMixingStraightAndArcSegmentsJoinsWithoutDuplicates()
    {
        var linearized = WktCurveLinearizer.Linearize("COMPOUNDCURVE ((0 0, 10 0), CIRCULARSTRING (10 0, 15 5, 20 0), (20 0, 30 0))");
        var points = ParseLineString(linearized);

        Assert.AreEqual((0d, 0d), points[0]);
        Assert.AreEqual((30d, 0d), points[^1]);
        for (var i = 1; i < points.Count; i++)
            Assert.AreNotEqual(points[i - 1], points[i], "junction points are not duplicated");
    }

    [TestMethod]
    public void MultiCurveBecomesMultiLineString()
    {
        var linearized = WktCurveLinearizer.Linearize("MULTICURVE ((0 0, 1 1), COMPOUNDCURVE ((2 2, 3 3)))");
        Assert.AreEqual("MULTILINESTRING ((0 0, 1 1), (2 2, 3 3))", linearized);
    }

    [TestMethod]
    public void MultiSurfaceBecomesMultiPolygon()
    {
        var linearized = WktCurveLinearizer.Linearize("MULTISURFACE (CURVEPOLYGON ((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))");
        Assert.AreEqual("MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))", linearized);
    }

    [TestMethod]
    public void CurvePolygonWithArcRingStaysClosed()
    {
        var linearized = WktCurveLinearizer.Linearize("CURVEPOLYGON (COMPOUNDCURVE ((0 0, 10 0), CIRCULARSTRING (10 0, 5 5, 0 0)))");
        StringAssert.StartsWith(linearized, "POLYGON ((");
        var points = ParsePointList(linearized);
        Assert.AreEqual(points[0], points[^1], "the linearized ring is closed");
        Assert.IsTrue(points.Count > 4, "the arc ring is stroked into many short segments");
    }

    private static List<(double X, double Y)> ParseLineString(string wkt)
    {
        StringAssert.StartsWith(wkt, "LINESTRING (");
        return ParsePointList(wkt);
    }

    private static List<(double X, double Y)> ParsePointList(string wkt)
    {
        var open = wkt.IndexOf('(');
        var body = wkt[(open + 1)..wkt.LastIndexOf(')')].Trim().TrimStart('(').TrimEnd(')');
        return body.Split(',')
            .Select(token => token.Trim().Split(' '))
            .Select(ordinates => (
                double.Parse(ordinates[0], CultureInfo.InvariantCulture),
                double.Parse(ordinates[1], CultureInfo.InvariantCulture)))
            .ToList();
    }
}
