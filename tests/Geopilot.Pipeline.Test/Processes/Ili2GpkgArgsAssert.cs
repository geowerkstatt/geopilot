using Geopilot.PipelineCore.Ilitools;
using System.Reflection;

namespace Geopilot.Pipeline.Test.Processes;

internal static class Ili2GpkgArgsAssert
{
    /// <summary>
    /// Asserts that every constructor parameter of <typeparamref name="TProcess"/> that is not one of the
    /// <paramref name="dependencies"/> sets the <see cref="Ili2GpkgArgs"/> property of the same name. Builds the
    /// process once per parameter with only that parameter set and the dependencies injected by parameter name.
    /// </summary>
    /// <param name="dependencies">The constructor arguments that are not tool arguments, keyed by parameter name.</param>
    /// <param name="runAndCaptureArgs">Runs the process and returns the arguments it handed to the client.</param>
    /// <typeparam name="TProcess">The process type to test.</typeparam>
    public static async Task SetsTheArgumentNamedAfterEachConstructorParameter<TProcess>(Dictionary<string, object?> dependencies, Func<TProcess, Task<Ili2GpkgArgs>> runAndCaptureArgs)
    {
        // The pipeline binds the configuration to the constructor parameters by name, so a parameter named after a
        // tool argument has to set exactly that argument.
        var constructor = typeof(TProcess).GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var checkedParameters = 0;

        foreach (var parameter in parameters)
        {
            if (dependencies.ContainsKey(parameter.Name ?? string.Empty))
                continue;

            var property = typeof(Ili2GpkgArgs).GetProperty(parameter.Name ?? string.Empty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.IsNotNull(property, $"No tool argument matches the constructor parameter <{parameter.Name}>.");

            var arguments = parameters
                .Select(p => p == parameter ? SampleValue(parameter.ParameterType) : dependencies.GetValueOrDefault(p.Name ?? string.Empty))
                .ToArray();
            var process = (TProcess)constructor.Invoke(arguments);

            var value = property.GetValue(await runAndCaptureArgs(process));
            if (value is bool flag)
                Assert.IsTrue(flag, $"Parameter <{parameter.Name}> did not set <{property.Name}>.");
            else
                Assert.IsNotNull(value, $"Parameter <{parameter.Name}> did not set <{property.Name}>.");

            checkedParameters++;
        }

        Assert.AreNotEqual(0, checkedParameters, "No constructor parameter matched a tool argument.");
    }

    private static object SampleValue(Type parameterType) => parameterType switch
    {
        _ when parameterType == typeof(bool?) => true,
        _ when parameterType == typeof(string) => "value",
        _ => throw new NotSupportedException($"No sample value for a parameter of type <{parameterType}>."),
    };
}
