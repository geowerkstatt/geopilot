# ili2gpkg-worker Docker image

Long-running worker that converts XTF files into GeoPackage files via the bundled
[ili2gpkg](https://github.com/claeis/ili2db) CLI. Replaces the previous pattern where
geopilot itself spawned one-shot ili2gpkg containers (which required mounting the Docker
socket into the geopilot container — a root-equivalent escalation surface).

## File-drop protocol

Two shared directories, bind-mounted into both geopilot and the worker:

```
${ILI2GPKG_INPUT_DIR}/
├── <correlationId>.xtf           written by geopilot (final name, no rename)
├── <correlationId>.config.json   written by geopilot (final name, no rename)
│                                 contains { originalFileName, import, models,
│                                            defaultSrsCode, ... }
└── <correlationId>.input.ready   zero-byte sentinel, written LAST by geopilot
${ILI2GPKG_OUTPUT_DIR}/
├── <correlationId>.gpkg          written by the worker on success
├── <correlationId>.error         written by the worker on failure (contains stderr)
└── <correlationId>.output.ready  zero-byte sentinel, written LAST by the worker
```

Geopilot picks a `Guid` for every invocation as the `<correlationId>`. It writes the xtf
and config sidecar directly under their final names, then drops a zero-byte
`<correlationId>.input.ready` sentinel last. The worker only consumes a job once it
observes the input sentinel, so a half-written xtf or config can never be processed —
the contract does not depend on rename atomicity, which is unreliable on bind-mounted
filesystems (Docker Desktop on Windows: virtiofs/9p/SMB).

The output side mirrors the same shape: the worker writes either `<correlationId>.gpkg`
or `<correlationId>.error` directly, then drops a zero-byte
`<correlationId>.output.ready` sentinel last. Geopilot polls only for the output
sentinel; once observed, it briefly waits for the corresponding payload to become
visible and stable in size before consuming.

The worker deletes input files (xtf, config, input.ready) after a successful conversion;
output files (gpkg, output.ready) are deleted by geopilot once consumed. On failure the
worker keeps the inputs for diagnostics — the orphan sweep eventually reclaims them.

## Orphan cleanup

The worker sweeps `${ILI2GPKG_OUTPUT_DIR}` on startup and removes files older than
`${ILI2GPKG_ORPHAN_MAX_AGE_MINUTES}` (default 1440 — 24 hours). That handles the
"geopilot died before consuming its output" case. There is no input sweep: if an input
XTF is still present, the next watcher pass reprocesses it (process.sh short-circuits
if the output already exists, making this idempotent).

## Running locally

Build:

```shell
docker build -t ili2gpkg-worker docker/ili2gpkg-worker
```

Run (pairs with a geopilot that writes into the same host paths):

```shell
docker run --rm \
    -v ./shared/ili2gpkg/in:/shared/ili2gpkg/in \
    -v ./shared/ili2gpkg/out:/shared/ili2gpkg/out \
    -v ili2gpkg-cache:/var/cache/ili2gpkg \
    ili2gpkg-worker
```

In docker-compose the worker is wired up next to the `geopilot` service with the same
pair of host directories bind-mounted into both.

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `ILI2GPKG_INPUT_DIR` | `/shared/ili2gpkg/in` | Directory watched for new `*.xtf` files |
| `ILI2GPKG_OUTPUT_DIR` | `/shared/ili2gpkg/out` | Directory where `*.gpkg` / `*.error` are written |
| `ILI2GPKG_CACHE_DIR` | `/var/cache/ili2gpkg` | Persistent INTERLIS model cache (named volume) |
| `ILI2GPKG_ORPHAN_MAX_AGE_MINUTES` | `1440` | Orphan-output sweep age threshold |
| `ILI2GPKG_POLL_FALLBACK_SECONDS` | `2` | inotifywait timeout — safety-net rescan interval for filesystems with unreliable inotify (e.g. Docker Desktop on Windows bind mounts) |

## Bumping the ili2gpkg version

`Dockerfile` has `ARG ILI2GPKG_VERSION=5.1.0` which controls the release downloaded
from `https://downloads.interlis.ch/ili2gpkg/` at build time. To bump:

1. Update `ARG ILI2GPKG_VERSION=` in `docker/ili2gpkg-worker/Dockerfile` (see
   [claeis/ili2db releases](https://github.com/claeis/ili2db/releases) for available versions).
2. Commit and push to `main`. The [`publish-ili2gpkg-worker.yml`](../../.github/workflows/publish-ili2gpkg-worker.yml)
   workflow rebuilds and pushes the image to GHCR automatically (path-filtered, so
   only changes under `docker/ili2gpkg-worker/**` trigger it). A one-off build against
   an arbitrary version can also be triggered manually via the workflow's
   `workflow_dispatch` input.
3. For local rebuilds, use `docker compose build ili2gpkg-worker`.

No code change needed in geopilot — the worker is self-contained from the API's
perspective, and the ili2gpkg CLI flags this worker uses are stable across releases.

## Published image

`ghcr.io/geowerkstatt/ili2gpkg-worker`. Tags published on every `main` build:

| Tag | Semantics |
|---|---|
| `:latest` | Most recent main build. Mutable. |
| `:<ili2gpkg-version>` | E.g. `:5.1.0` — tracks the ili2gpkg version the image bundles. Mutable; rebuilt when the wrapper changes against that version. |
| `:<ili2gpkg-version>-sha-<short>` | E.g. `:5.1.0-sha-abc1234` — immutable pin to a specific wrapper revision of a given ili2gpkg release. Use this in production for reproducible deployments. |
