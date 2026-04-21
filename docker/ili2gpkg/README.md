# ili2gpkg Docker image

Wraps [ili2gpkg](https://github.com/claeis/ili2db) — the Eisenhut Informatik CLI tool for converting between INTERLIS transfer files (XTF/ITF) and GeoPackage — for use by `IliToGeoPackageProcess` in the GeoPilot pipeline.

Published as `ghcr.io/geowerkstatt/ili2gpkg:<version>`.

## Bumping the ili2gpkg version

1. Update `ARG ILI2GPKG_VERSION=` in [`Dockerfile`](./Dockerfile) to the desired release (see [claeis/ili2db releases](https://github.com/claeis/ili2db/releases))
2. Update the `Image` constant in `src/Geopilot.Api/Pipeline/Process/IliToGeoPackage/IliToGeoPackageProcess.cs` to match
3. Commit and push to `main` — the `publish-ili2gpkg.yml` workflow builds and pushes the image automatically (path-filtered so only changes to `docker/ili2gpkg/**` trigger it)

A one-off build against any version can also be triggered manually via the workflow's `workflow_dispatch` input.

## Running locally

```shell
docker run --rm -v "$PWD:/data" ghcr.io/geowerkstatt/ili2gpkg:5.1.0 \
  --import --dbfile /data/out.gpkg /data/input.xtf
```
