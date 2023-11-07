using NetTopologySuite.Geometries;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A contract between the system owner and an organisation for data delivery.
    /// The mandate describes where and in what format data should be delivered.
    /// </summary>
    public class DeliveryMandate
    {
        /// <summary>
        /// The unique identifier for the mandate.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The display name of the mandate.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// List of file types that are allowed to be delivered. Include the period "." and support wildcards "*".
        /// </summary>
        public string[] FileTypes { get; set; } = new[] { ".*" };

        /// <summary>
        /// The spatial extent of the mandate. The extent is a polygon in WGS84.
        /// Deliverd data must be within the extent.
        /// </summary>
        public Geometry SpatialExtent { get; set; } = GeometryFactory.Default.CreatePolygon();

        /// <summary>
        /// Organisations allowed to deliver data fulfilling the mandate.
        /// </summary>
        public List<Organisation> Organisations { get; set; } = new List<Organisation>();

        /// <summary>
        /// Data deliveries that have been declared fulfilling the mandate.
        /// </summary>
        public List<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
