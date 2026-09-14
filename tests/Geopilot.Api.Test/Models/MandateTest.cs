using NetTopologySuite.Geometries;

namespace Geopilot.Api.Models;

[TestClass]
public class MandateTest
{
    [TestMethod]
    [DataRow(8.86, 46.70, 7.93, 47.02, DisplayName = "lower left is right of upper right")]
    [DataRow(7.93, 47.02, 8.86, 46.70, DisplayName = "lower left is above upper right")]
    [DataRow(7.93, 46.70, 7.93, 47.02, DisplayName = "extent has no area")]
    public void SetPolygonFromCoordinatesRejectsInvalidCorners(double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
    {
        var mandate = CreateMandate(lowerLeftX, lowerLeftY, upperRightX, upperRightY);

        Assert.IsFalse(mandate.SetPolygonFromCoordinates());
    }

    [TestMethod]
    public void SetCoordinateListFromPolygonKeepsStoredCorners()
    {
        var mandate = new Mandate { SpatialExtent = CreateRing(8.86, 47.02, 7.93, 46.70) };

        mandate.SetCoordinateListFromPolygon();

        Assert.HasCount(2, mandate.Coordinates);
        AssertCoordinate(8.86, 47.02, mandate.Coordinates[0]);
        AssertCoordinate(7.93, 46.70, mandate.Coordinates[1]);
    }

    [TestMethod]
    public void SetCoordinateListFromPolygonRoundTripsCoordinates()
    {
        var mandate = CreateMandate(7.93, 46.70, 8.86, 47.02);
        Assert.IsTrue(mandate.SetPolygonFromCoordinates());

        mandate.SetCoordinateListFromPolygon();

        Assert.HasCount(2, mandate.Coordinates);
        AssertCoordinate(7.93, 46.70, mandate.Coordinates[0]);
        AssertCoordinate(8.86, 47.02, mandate.Coordinates[1]);
    }

    private static Mandate CreateMandate(double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY) =>
        new()
        {
            Coordinates = new List<Coordinate>
            {
                new() { X = lowerLeftX, Y = lowerLeftY },
                new() { X = upperRightX, Y = upperRightY },
            },
        };

    private static Polygon CreateRing(double x0, double y0, double x1, double y1) =>
        Geometry.DefaultFactory.CreatePolygon(new NetTopologySuite.Geometries.Coordinate[]
        {
            new(x0, y0),
            new(x0, y1),
            new(x1, y1),
            new(x1, y0),
            new(x0, y0),
        });

    private static void AssertCoordinate(double expectedX, double expectedY, Coordinate actual)
    {
        Assert.AreEqual(expectedX, actual.X);
        Assert.AreEqual(expectedY, actual.Y);
    }
}
