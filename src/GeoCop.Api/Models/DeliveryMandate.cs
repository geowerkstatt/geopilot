using NetTopologySuite.Geometries;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A contract between the system owner and an organisation for data delivery.
    /// The mandate describes where and in what format data should be delivered.
    /// </summary>
    internal class DeliveryMandate
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string[] FileTypes { get; set; } = new[] { ".*" };

        public Geometry SpatialExtent { get; set; } = GeometryFactory.Default.CreatePolygon();

        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
