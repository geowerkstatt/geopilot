# XTF-Diff Worker Service

Long-running worker that runs the bundled [XTF-Diff-Tool](https://github.com/geowerkstatt/XTF-Diff-Tool)
CLI over per-request job folders dropped into a shared directory by any client. It
compares two INTERLIS XTF files and produces a JSON diff describing the differences
(see the tool's [OutputFileDescription.md](https://github.com/geowerkstatt/XTF-Diff-Tool/blob/main/OutputFileDescription.md)).

The worker is a self-contained service. Clients interact with it purely through the
shared filesystem — no network protocol, no socket, no SDK. Anything that can write
files into the jobs directory and poll for sentinels can drive it. The geopilot
pipeline client is `Geopilot.Api.Pipeline.Process.XtfDiff.XtfDiffProcess`.

## File-drop protocol

A single shared directory is bind-mounted into both the worker and the client. Each
request gets its own subfolder (named by the client with any unique id, typically a
UUID). Payload files use **fixed names defined by the protocol** — the worker does not
accept caller-supplied names. This keeps the surface clients can influence inside the
shared mount minimal.

```
${XTF_DIFF_JOBS_DIR}/
└── <jobId>/
    ├── args.json               written by the client — optional tool flags
    ├── old.xtf                 the previous state of the data — input
    ├── new.xtf                 the current state of the data — input
    ├── diff.json               the JSON diff — output, written by the worker
    ├── input.ready             zero-byte sentinel, written LAST by the client
    ├── output.ready            zero-byte sentinel, written LAST by the worker
    ├── success.log             written by the worker on success — captures the entire
    │                           XTF-Diff-Tool stdout + stderr for this job
    └── error.log               written by the worker on failure — captures the entire
                                XTF-Diff-Tool stdout + stderr (or the worker-side
                                validation message if the tool never ran) for this job
```

The client picks any unique `<jobId>`, creates the folder, drops all the files, then
writes a zero-byte `input.ready` sentinel inside the folder last. The worker only
consumes a job once it observes that sentinel, so a half-written payload can never be
processed — the contract does not depend on rename atomicity, which is unreliable on
bind-mounted filesystems (Docker Desktop on Windows: virtiofs/9p/SMB).

The output side mirrors the same shape: the worker writes `diff.json` plus a
`success.log` (or `error.log` on failure — there is no separate "error" sentinel; the
log file is the source of truth), then drops a zero-byte `output.ready` sentinel last.
The client polls only for the output sentinel; once observed, it reads `diff.json` on
success and `error.log` on failure to surface the diagnostic.

The client should cleanup the job folder after it has consumed all results.
If the client does not cleanup the job folder, the worker will clean it up after
the configured period of time.

## `args.json`

Field names mirror XTF-Diff-Tool CLI flags verbatim, so an operator reading
`args.json` sees exactly what flags will be applied. Filenames are not part of
`args.json` — they are fixed by the protocol. The file is optional; without it the
tool runs with its defaults.

```json
{
  "modeldir": "https://models.interlis.ch/"
}
```

| Field      | XTF-Diff-Tool flag | Notes |
|------------|--------------------|-------|
| `modeldir` | `--modeldir`       | INTERLIS model search directories |

## Cleanup

The worker sweeps `${XTF_DIFF_JOBS_DIR}` on startup and removes folders where nothing
happened in the last `${XTF_DIFF_ORPHAN_MAX_AGE_MINUTES}` (default 1440 — 24 hours).
That handles the cases, where the worker dies during a job or where the client never
deletes the job assets.

## Running locally

Build:

```shell
docker build -t xtf-diff-worker docker/xtf-diff-worker
```

Run (pairs with any client that writes into the same host path):

```shell
docker run --rm \
    -v ./shared/xtf-diff/jobs:/shared/xtf-diff/jobs \
    -v xtf-diff-cache:/var/cache/xtf-diff-tool \
    xtf-diff-worker
```

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `XTF_DIFF_JOBS_DIR` | `/shared/xtf-diff/jobs` | Directory watched (recursively) for `<jobId>/input.ready` sentinels |
| `XTF_DIFF_CACHE_DIR` | `/var/cache/xtf-diff-tool` | Persistent INTERLIS model cache (named volume) |
| `XTF_DIFF_ORPHAN_MAX_AGE_MINUTES` | `1440` | Orphan-folder sweep age threshold |
| `XTF_DIFF_POLL_FALLBACK_SECONDS` | `2` | inotifywait timeout — safety-net rescan interval for filesystems with unreliable inotify (e.g. Docker Desktop on Windows bind mounts) |

## Bumping the XTF-Diff-Tool version

`Dockerfile` has `ARG XTF_DIFF_TOOL_VERSION=v1.0.19` which controls the base image tag
pulled from `ghcr.io/geowerkstatt/xtf-diff-tool` at build time. To bump:

1. Update `ARG XTF_DIFF_TOOL_VERSION=` in `docker/xtf-diff-worker/Dockerfile` (see
   [geowerkstatt/XTF-Diff-Tool releases](https://github.com/geowerkstatt/XTF-Diff-Tool/releases)
   for available versions).
2. Commit and push to `main`. The [`publish-xtf-diff-worker.yml`](../../.github/workflows/publish-xtf-diff-worker.yml)
   workflow rebuilds and pushes the image to GHCR automatically (path-filtered, so
   only changes under `docker/xtf-diff-worker/**` trigger it). A one-off build against
   an arbitrary version can also be triggered manually via the workflow's
   `workflow_dispatch` input.
3. For local rebuilds, use `docker compose build xtf-diff-worker`.
