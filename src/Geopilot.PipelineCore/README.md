# GeoWerkstatt.Geopilot.PipelineCore

Core pipeline abstractions for [geopilot](https://github.com/GeoWerkstatt/geopilot) — a full-stack geodata validation and delivery tool.

This package contains the public interfaces and base types that plugin authors use to implement custom pipeline processes (matchers, validators, transformers, delivery steps) for geopilot.

## Installation

This package is published to the [GeoWerkstatt GitHub Packages NuGet registry](https://github.com/GeoWerkstatt/geopilot/packages).

Add the GitHub Packages source to your project (requires a [GitHub PAT](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry) with the `read:packages` scope):

```shell
dotnet nuget add source https://nuget.pkg.github.com/GeoWerkstatt/index.json \
  --name github-geowerkstatt \
  --username <your-github-username> \
  --password <your-github-pat> \
  --store-password-in-clear-text
```

Then install the package:

```shell
dotnet add package GeoWerkstatt.Geopilot.PipelineCore
```

## Usage

Implement a custom pipeline process by referencing the abstractions from this package:

```csharp
using Geopilot.PipelineCore.Pipeline;

public class MyCustomProcess
{
    [PipelineProcessRun]
    public Task<MyCustomProcessResult> RunAsync(IPipelineFile[] files, CancellationToken cancellationToken)
    {
        // The pipeline definition wires this parameter to any source, e.g. files: "${upload()}"
        // ... your logic
        return Task.FromResult(new MyCustomProcessResult { Result = "..." });
    }
}

// Every public property of the result is an output, referenced from a later step by its
// PascalCase name, for example "${step_output(myStep.Result)}".
public class MyCustomProcessResult
{
    public required string Result { get; set; }
}
```

Documentation:

- [Plugin System](https://github.com/GeoWerkstatt/geopilot/blob/main/docs/pipeline/pluginSystem.md): the plugin contract, processor requirements and how to load a plugin into geopilot.
- [Pipelines](https://github.com/GeoWerkstatt/geopilot/blob/main/docs/pipeline/Pipelines.md): the pipeline concepts and the YAML definition format a processor is wired into.

## License

Licensed under the [AGPL-3.0-or-later](https://www.gnu.org/licenses/agpl-3.0.html) license.
