namespace Geopilot.Api.Models
{
    /// <summary>
    /// Defines how <see cref="Delivery"/> fileds have to be evaluated.
    /// </summary>
    public enum FieldEvaluationType
    {
        /// <summary>
        /// Field must not contain any value.
        /// </summary>
        NotEvaluated,

        /// <summary>
        /// Field may contain a value but must not.
        /// </summary>
        Optional,

        /// <summary>
        /// Field must contan a value.
        /// </summary>
        Required,
    }
}
