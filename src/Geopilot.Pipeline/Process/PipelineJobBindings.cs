using Geopilot.Pipeline.Ilitools;

namespace Geopilot.Pipeline.Process;

/// <summary>
/// What the host binds to one job when it starts, besides its files: data it reads for the job, e.g. from its
/// database. A process asks for a binding with a constructor parameter of the binding's interface and gets
/// <see langword="null"/> where the host bound none. To add a binding, define its interface and list it in
/// <see cref="BindingTypes"/>; the process factory and the startup check pick it up from there.
/// </summary>
internal sealed class PipelineJobBindings
{
    private static readonly HashSet<Type> BindingTypes = [
        typeof(IRefMappings),
    ];

    private readonly List<object> bindings;

    /// <summary>
    /// No bindings at all, what a job gets from a caller outside the host.
    /// </summary>
    public static PipelineJobBindings None { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineJobBindings"/> class.
    /// </summary>
    /// <param name="bindings">The bindings of the job, at most one per binding type.</param>
    /// <exception cref="ArgumentException">A binding implements none or several of the binding types, or two
    /// bindings implement the same one.</exception>
    public PipelineJobBindings(params IEnumerable<object> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        this.bindings = bindings.ToList();
        var bindingTypes = this.bindings.Select(BindingTypeOf).ToList();
        if (bindingTypes.Count != bindingTypes.Distinct().Count())
            throw new ArgumentException("A job takes at most one binding per binding type.", nameof(bindings));
    }

    /// <summary>
    /// Whether a constructor parameter of <paramref name="type"/> receives a binding instead of a configured value.
    /// </summary>
    public static bool IsBindingType(Type type) => BindingTypes.Contains(type);

    /// <summary>
    /// The binding of <paramref name="bindingType"/>, or <see langword="null"/> where the host bound none.
    /// </summary>
    public object? Get(Type bindingType)
    {
        ArgumentNullException.ThrowIfNull(bindingType);

        return bindings.FirstOrDefault(bindingType.IsInstanceOfType);
    }

    private static Type BindingTypeOf(object binding)
    {
        var types = BindingTypes.Where(type => type.IsInstanceOfType(binding)).ToList();
        return types.Count == 1
            ? types[0]
            : throw new ArgumentException($"<{binding.GetType().FullName}> must implement exactly one binding type.", nameof(binding));
    }
}
