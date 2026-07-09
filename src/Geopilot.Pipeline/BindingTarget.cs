using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Describes the process run method parameter an input value is bound to: its name, its type and
/// whether the parameter (and, for an array, its element) accepts null. Used by
/// <see cref="InputBinder"/> to decide how to coerce a resolved value.
/// </summary>
internal sealed record BindingTarget(string Name, Type Type, bool IsNullable, bool IsElementNullable)
{
    /// <summary>
    /// Builds a <see cref="BindingTarget"/> from a run method parameter, reading its nullability
    /// from the compiler generated annotations.
    /// </summary>
    /// <param name="parameter">The process run method parameter to describe.</param>
    /// <returns>The binding target for the parameter.</returns>
    internal static BindingTarget FromParameter(ParameterInfo parameter)
    {
        var nullability = new NullabilityInfoContext().Create(parameter);
        return new BindingTarget(
            parameter.Name ?? string.Empty,
            parameter.ParameterType,
            nullability.WriteState is NullabilityState.Nullable,
            nullability.ElementType?.WriteState is NullabilityState.Nullable);
    }
}
