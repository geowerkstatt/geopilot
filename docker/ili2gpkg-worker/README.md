# ili2gpkg Worker Service

Long-running worker that runs the bundled [ili2gpkg](https://github.com/claeis/ili2db)
CLI over per-request job folders dropped into a shared directory by any client.
Supports all three ili2gpkg verbs:

- `--schemaimport` — create GeoPackage schema from an INTERLIS model.
- `--import` — import data from an XTF into an existing GeoPackage.
- `--export` — export data from a GeoPackage back to an XTF.

The worker is a self-contained service. Clients interact with it purely through the
shared filesystem — no network protocol, no socket, no SDK. Anything that can write
files into the jobs directory and poll for sentinels can drive it.

## File-drop protocol

A single shared directory is bind-mounted into both the worker and the client. Each
request gets its own subfolder (named by the client with any unique id, typically a
UUID). Payload files use **fixed names defined by the protocol** — the worker does not
accept caller-supplied names. This keeps the surface clients can influence inside the
shared mount minimal.

```
${ILI2GPKG_JOBS_DIR}/
└── <jobId>/
    ├── args.json               written by the client — operation + flags
    ├── model.ili               INTERLIS model — input for schemaimport
    ├── data.xtf                XTF data — input for import; output for export
    ├── dbfile.gpkg             GeoPackage — output for schemaimport; input/output
    │                           for import (written in place); input for export
    ├── input.ready             zero-byte sentinel, written LAST by the client
    ├── output.ready            zero-byte sentinel, written LAST by the worker
    ├── success.log             written by the worker on success — captures the entire
    │                           ili2gpkg stdout + stderr for this job
    └── error.log               written by the worker on failure — captures the entire
                                ili2gpkg stdout + stderr (or the worker-side validation
                                message if ili2gpkg never ran) for this job
```

Only the file(s) relevant to the requested operation need to be present. The full
per-operation file shape is summarised in the args.json section below.

The client picks any unique `<jobId>`, creates the folder, drops all the files, then
writes a zero-byte `input.ready` sentinel inside the folder last. The worker only
consumes a job once it observes that sentinel, so a half-written payload can never be
processed — the contract does not depend on rename atomicity, which is unreliable on
bind-mounted filesystems (Docker Desktop on Windows: virtiofs/9p/SMB).

The output side mirrors the same shape: the worker writes the result file in place
(modifying the gpkg for schemaimport/import, creating the xtf for export) plus a
`success.log` (or `error.log` on failure — there is no separate "error" sentinel; the
log file is the source of truth), then drops a zero-byte `output.ready` sentinel last.
The client polls only for the output sentinel; once observed, it briefly waits for the
result file to become visible and stable in size before consuming, and on failure
reads `error.log` to surface the diagnostic.

The client should cleanup the job folder after it has consumed all results.
If the client does not cleanup the job folder, the worker will clean it up after
the configured period of time.

## `args.json`

Field names mirror `ili2gpkg` CLI flags verbatim, so an operator reading `args.json`
sees exactly what flags will be applied. The only field that is not a flag-mirror is
`operation` (the dispatch enum). Filenames are not part of `args.json` — they are
fixed by the protocol.

```json
{
  "operation": "import",
  "models": ["MyModel"],
  "disableValidation": false,
  "createBasketCol": true,
  "smart2Inheritance": true,
  "defaultSrsCode": 2056
}
```

| Field               | ili2gpkg flag         | Notes |
|---------------------|-----------------------|-------|
| `operation`           | (dispatch only)         | `schemaimport` \| `import` \| `export` |
| `models`              | `--models`              | passed as semicolon-joined list |
| `disableValidation`   | `--disableValidation`   | |
| `createBasketCol`     | `--createBasketCol`     | |
| `defaultSrsCode`      | `--defaultSrsCode`      | |
| `sqlEnableNull`       | `--sqlEnableNull`       | |
| `skipReferenceErrors` | `--skipReferenceErrors` | |
| `skipGeometryErrors`  | `--skipGeometryErrors`  | |
| `importTid`           | `--importTid`           | |
| `strokeArcs`          | `--strokeArcs`          | |

Per-operation file shape inside the job folder:

| Operation       | Client writes                           | Worker produces |
|-----------------|-----------------------------------------|-----------------|
| `schemaimport`  | `model.ili`                             | `dbfile.gpkg`   |
| `import`        | `data.xtf` + `dbfile.gpkg` (with schema)| `dbfile.gpkg` (modified in place) |
| `export`        | `dbfile.gpkg`                           | `data.xtf`      |

The worker does not pre-validate the presence/absence of payload files: ili2gpkg
itself reports any missing input via its own stderr, which the worker captures into
`error.log`.

## Cleanup

The worker sweeps `${ILI2GPKG_JOBS_DIR}` on startup and removes folders where nothing
happened in the last `${ILI2GPKG_ORPHAN_MAX_AGE_MINUTES}` (default 1440 — 24 hours).
That handles the cases, where the worker dies during a job or where the client never
deletes the job assets.

## Running locally

Build:

```shell
docker build -t ili2gpkg-worker docker/ili2gpkg-worker
```

Run (pairs with any client that writes into the same host path):

```shell
docker run --rm \
    -v ./shared/ili2gpkg/jobs:/shared/ili2gpkg/jobs \
    -v ili2gpkg-cache:/var/cache/ili2gpkg \
    ili2gpkg-worker
```

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `ILI2GPKG_JOBS_DIR` | `/shared/ili2gpkg/jobs` | Directory watched (recursively) for `<jobId>/input.ready` sentinels |
| `ILI2GPKG_CACHE_DIR` | `/var/cache/ili2gpkg` | Persistent INTERLIS model cache (named volume) |
| `ILI2GPKG_ORPHAN_MAX_AGE_MINUTES` | `1440` | Orphan-folder sweep age threshold |
| `ILI2GPKG_POLL_FALLBACK_SECONDS` | `2` | inotifywait timeout — safety-net rescan interval for filesystems with unreliable inotify (e.g. Docker Desktop on Windows bind mounts) |

## Image versioning and tags

The worker image is versioned independently of the bundled ili2gpkg release, so a change
to the wrapper (Dockerfile, `entrypoint.sh`, `process.sh`) produces a new image version
even when the ili2gpkg version is unchanged.

- The base version `MAJOR.MINOR` lives in [`VERSION`](VERSION); the published version is
  `MAJOR.MINOR.<run>`, where `<run>` is the CI run number of the publish workflow.
- Bump `MAJOR` for a breaking change to the file-drop protocol or the worker contract,
  `MINOR` for a backward-compatible change. The patch is assigned automatically.

Tags pushed to `ghcr.io/geowerkstatt/ili2gpkg-worker`:

| Tag | Mutability | Use |
|---|---|---|
| `:v<major>` (e.g. `:v1`) | rolling | Recommended production pin. Always resolves to the newest build of that major and survives retention. |
| `:v<major.minor.run>` (e.g. `:v1.0.42`) | immutable, short-lived | Granular per-build reference. Pruned shortly after the next build by the retention step. |
| `:latest` | rolling | Most recent build across all majors. Not recommended for production. |

The bundled ili2gpkg version is not part of the tag; it is recorded in the
`ch.geowerkstatt.ili2gpkg.version` image label (`org.opencontainers.image.version` carries
the worker version). Inspect both with:

```shell
docker buildx imagetools inspect ghcr.io/geowerkstatt/ili2gpkg-worker:v1
```

Retention is aggressive: the publish workflow keeps only the newest few image digests and
prunes older granular tags and untagged manifests. Because exact `:v<major.minor.run>` tags
are short-lived, pin the rolling `:v<major>` tag in production.

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

Bumping ili2gpkg does not require a manual worker version change: the next build picks up a
new `MAJOR.MINOR.<run>` automatically and records the new ili2gpkg version in the
`ch.geowerkstatt.ili2gpkg.version` label. Consider a `MINOR` bump in [`VERSION`](VERSION) for
a notable ili2gpkg upgrade so the jump is visible in the worker version too.
