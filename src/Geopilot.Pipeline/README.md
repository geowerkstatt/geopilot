# GeoWerkstatt.Geopilot.Pipeline

Pipeline runtime for [geopilot](https://github.com/GeoWerkstatt/geopilot), a full-stack geodata validation and delivery tool.

This package contains the runtime that reads a pipeline definition, instantiates the configured processes and executes their steps.

**To write a processor you do not need this package.** Implement it against [`GeoWerkstatt.Geopilot.PipelineCore`](https://github.com/GeoWerkstatt/geopilot/blob/main/src/Geopilot.PipelineCore/README.md), which holds the plugin contract and is versioned independently. This package is what you add on top when you want to run a processor through a real pipeline in your integration tests, instead of calling its run method directly.

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
dotnet add package GeoWerkstatt.Geopilot.Pipeline
```

## Usage

Build a pipeline from a definition, take the step under test and run it against a prepared context. The context supplies the uploaded files and the results of earlier steps, so a step can be exercised in isolation without running the whole pipeline:

```csharp
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

// Stands in for the result of the upstream step. Only the property the definition
// references via ${step_output(upstreamStep.GeneratedFile)} has to exist.
private sealed record UpstreamStepResult(IPipelineFile GeneratedFile);

var step = pipeline.Steps.Single(s => s.Id == "myStep");

var context = new PipelineContext
{
    Upload = [],
    StepResults = new Dictionary<string, StepResult>
    {
        ["upstreamStep"] = new() { Result = new UpstreamStepResult(CreateTestFile()) },
    },
};

var result = await step.Run(context, CancellationToken.None);

Assert.AreEqual(StepState.Success, step.State);
```

Running the step through the pipeline rather than calling the run method directly is what exercises the parts a plugin cannot test on its own: the constructor binding from `default_config`, the input binding from the definition, and the evaluation of the step's conditions.

Documentation:

- [Plugin System](https://github.com/GeoWerkstatt/geopilot/blob/main/docs/pipeline/pluginSystem.md): the plugin contract, processor requirements and how to load a plugin into geopilot.
- [Pipelines](https://github.com/GeoWerkstatt/geopilot/blob/main/docs/pipeline/Pipelines.md): the pipeline concepts and the YAML definition format a processor is wired into.

## License

Licensed under the [AGPL-3.0-or-later](https://www.gnu.org/licenses/agpl-3.0.html) license.
