# Hop Worker Service

Long-running worker that runs Apache Hop pipelines (`.hpl`) and workflows (`.hwf`) over
per-request job folders dropped into a shared directory by any client. The bundled Hop
distribution carries the GIS and geometry plugins the geopilot pipelines need.

The worker is a self-contained service. Clients interact with it purely through the
shared filesystem: no network protocol, no socket, no SDK. Anything that can write files
into the jobs directory and poll for sentinels can drive it. In geopilot the client is
[`HopClient.cs`](../../src/Geopilot.Pipeline/Processes/Hop/HopClient.cs).

This worker follows the same file-drop protocol as the `ili2gpkg-worker`;
[`docker/ili2gpkg-worker/README.md`](../ili2gpkg-worker/README.md) is the canonical,
fully-documented protocol spec. This file documents the Hop-specific parts: the job
layout, `args.json`, and the bundled Hop version and plugins.

## File-drop protocol

A single shared directory is bind-mounted into both the worker and the client. Each
request gets its own subfolder, named by the client with any unique id (typically a
UUID). Unlike the ili2gpkg worker, which uses fixed payload filenames in the job root,
the Hop worker uses an `input/` and an `output/` subdirectory so a pipeline can consume
and produce an arbitrary file tree.

```
${HOP_JOBS_DIR}/
└── <jobId>/
    ├── args.json        (client) pipeline reference + Hop parameters
    ├── input/           (client) payload files, relative hierarchy preserved
    ├── output/          (worker) the files the pipeline produced
    ├── input.ready      (client, written LAST) zero-byte job-ready sentinel
    ├── output.ready     (worker, written LAST) zero-byte completion sentinel
    ├── success.log      (worker, on success) the entire Hop stdout + stderr
    └── error.log        (worker, on failure) the entire Hop stdout + stderr,
                         or the worker-side message if Hop never ran
```

The client picks any unique `<jobId>`, creates the folder, writes `input/` and
`args.json`, then writes a zero-byte `input.ready` sentinel last. The worker only
consumes a job once it observes that sentinel, so a half-written payload can never be
processed. The contract does not depend on rename atomicity, which is unreliable on
bind-mounted filesystems (Docker Desktop on Windows: virtiofs/9p).

The output side mirrors this: the worker runs the pipeline (which writes its results
into `output/`), writes `success.log` (or `error.log` on failure), then drops a
zero-byte `output.ready` sentinel last. There is no separate "error" sentinel; the log
file is the source of truth. The client polls only for `output.ready`, then collects
everything under `output/` and deletes the job folder.

If the client does not clean up, the worker reclaims the folder after the configured
orphan age (see Cleanup).

## `args.json`

```json
{
  "pipeline": "/shared/hop/pipelines/dmav_lfphfp2csv.hpl",
  "parameters": {
    "SOURCE_SRS": "2056",
    "TARGET_FORMAT": "gpkg"
  }
}
```

| Field        | Required | Notes |
|--------------|----------|-------|
| `pipeline`   | yes      | Absolute path to a Hop pipeline (`.hpl`) or workflow (`.hwf`). Definitions are not part of a job: they live in a separate, typically read-only, directory mounted into the worker (e.g. `/shared/hop/pipelines`) and are referenced here by path. |
| `parameters` | no       | Map of Hop named parameters, passed to `hop-run` as `--parameters=key=value,...`. Values must not contain commas (the worker comma-joins them). |

The worker always appends two more named parameters, `inputDir` and `outputDir`,
pointing at the job's `input/` and `output/` directories. A pipeline therefore reads its
inputs via `${inputDir}` and writes its results under `${outputDir}` and never hard-codes
job paths. The pipeline is run with `hop-run --runconfig=local`; a non-zero exit code is
treated as failure.

## Cleanup

The worker sweeps `${HOP_JOBS_DIR}` on startup and removes folders in which nothing was
modified within the last `${HOP_ORPHAN_MAX_AGE_MINUTES}` (default 1440, i.e. 24 hours).
That covers both a client that died before consuming its result and a worker that was
killed mid-job.

## Running locally

Through Docker Compose (mounts the pipeline definitions read-only and the shared jobs
folder):

```shell
docker compose up -d hop-worker
```

Or standalone, paired with any client that writes into the same host paths:

```shell
docker build -t hop-worker docker/hop-worker
docker run --rm \
    -v ./tests/Geopilot.Api.Test/TestData/HopPipelines:/shared/hop/pipelines:ro \
    -v ./src/Geopilot.Api/shared/hop/jobs:/shared/hop/jobs \
    hop-worker
```

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `HOP_JOBS_DIR` | `/shared/hop/jobs` | Directory watched (recursively) for `<jobId>/input.ready` sentinels. |
| `HOP_ORPHAN_MAX_AGE_MINUTES` | `1440` | Orphan-folder sweep age threshold. |
| `HOP_POLL_FALLBACK_SECONDS` | `2` | `inotifywait` timeout: safety-net rescan interval for filesystems with unreliable inotify (e.g. Docker Desktop on Windows bind mounts). |

The worker watches with `inotifywait` (`close_write`, `moved_to`, `create`) plus an
initial startup scan, and rescans on every event or fallback timeout. It dispatches
strictly on `input.ready`; events on payload files are ignored. `process.sh` is
idempotent: a folder that already has `output.ready` is skipped.

## Bundled Hop version and plugins

The image pins **Apache Hop 2.17.0** (`FROM apache/hop:2.17.0`) and bundles two
third-party plugins the geopilot pipelines depend on:

- **atolcd hop-gis-plugins** (`GIS_PLUGIN_VERSION`, currently `1.4.0`): GIS transforms,
  including `GisFileInput` for reading GeoPackage.
- **GeometryFieldsConverter**: a custom transform, fetched as a prebuilt artifact.

The Hop version is pinned on purpose. Both plugins are compiled against a specific Hop
plugin API and are not built for Hop 2.18+ (the atolcd GIS plugin's latest release
targets an older Hop, and the converter is a one-off build). On 2.18 these transforms
fail to load or run, which is why the worker is held at 2.17.0. Do not bump `apache/hop`
without first providing matching plugin builds: a newer atolcd GIS release for the target
Hop version and a GeometryFieldsConverter rebuilt against it. When a job fails after a
version change, the first exception in the job's `error.log` names the offending plugin.
