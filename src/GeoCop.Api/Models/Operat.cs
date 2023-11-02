using NetTopologySuite.Geometries;

namespace GeoCop.Api.Models
{
    internal class Operat
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string[] FileTypes { get; set; } = new[] { ".*" };

        public Geometry SpatialExtent { get; set; } = GeometryFactory.Default.CreatePolygon();

        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();

    }
}
