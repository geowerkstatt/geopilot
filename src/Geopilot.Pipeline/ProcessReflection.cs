using Geopilot.PipelineCore.Pipeline.Process;
using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Reflection over a process type's <c>[PipelineProcessRun]</c> run method. The rule "exactly one
/// [PipelineProcessRun] method" is enforced in several places, so the lookup lives here and callers
/// delegate to it.
/// </summary>
internal static class ProcessReflection
{
    /// <summary>
    /// All public instance methods on <paramref name="processType"/> annotated with
    /// <c>[PipelineProcessRun]</c>. A well-formed process has exactly one.
    /// </summary>
    internal static IReadOnlyList<MethodInfo> GetRunMethods(Type processType)
    {
        ArgumentNullException.ThrowIfNull(processType);
        return processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => Attribute.IsDefined(method, typeof(PipelineProcessRunAttribute)))
            .ToList();
    }

    /// <summary>
    /// The single <c>[PipelineProcessRun]</c> method of <paramref name="processType"/>, or
    /// <see langword="null"/> when it does not have exactly one.
    /// </summary>
    internal static MethodInfo? FindRunMethod(Type processType)
    {
        var runMethods = GetRunMethods(processType);
        return runMethods.Count == 1 ? runMethods[0] : null;
    }

    /// <summary>
    /// The <c>TResult</c> of the single run method's <c>Task&lt;TResult&gt;</c> return type, or
    /// <see langword="null"/> when there is no unique run method or it does not return a <c>Task&lt;TResult&gt;</c>.
    /// </summary>
    internal static Type? ResolveResultType(Type processType)
    {
        var runMethod = FindRunMethod(processType);
        if (runMethod is null)
            return null;

        var returnType = runMethod.ReturnType;
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            return null;

        return returnType.GetGenericArguments()[0];
    }
}
