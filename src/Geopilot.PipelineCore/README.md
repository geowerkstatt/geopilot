# GeoWerkstatt.Geopilot.PipelineCore

Core pipeline abstractions for [GeoPilot](https://github.com/GeoWerkstatt/geopilot) — a full-stack geodata validation and delivery tool.

This package contains the public interfaces and base types that plugin authors use to implement custom pipeline processes (matchers, validators, transformers, delivery steps) for GeoPilot.

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
    public Task<Dictionary<string, object?>> RunAsync([UploadFiles] IPipelineFileList uploadFiles)
    {
        // ... your logic
        return Task.FromResult(new Dictionary<string, object?>
        {
            { "result", "..." },
        });
    }
}
```

See the [GeoPilot repository](https://github.com/GeoWerkstatt/geopilot) for documentation and examples.

## License

Licensed under the [AGPL-3.0-or-later](https://www.gnu.org/licenses/agpl-3.0.html) license.
