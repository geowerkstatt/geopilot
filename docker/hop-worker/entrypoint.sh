#!/bin/sh
# hop-worker entrypoint.
#
# Watches ${HOP_JOBS_DIR} for new <jobId>/input.ready sentinel files
# and invokes process.sh for each containing folder. The sentinel is written last by
# the client, after all the job's payload files have been closed, so its appearance is
# a reliable "this job is ready to process" signal that does not depend on rename
# atomicity (which is unreliable on Docker Desktop bind-mounts to the host filesystem
# on Windows).
#
# Two event sources:
#   1. An initial scan on startup, to pick up sentinels that arrived while the worker was
#      restarting (or were left behind by a previous crashed run).
#   2. inotifywait -r for close_write + moved_to on the jobs root, so a fresh sentinel
#      triggers processing with sub-second latency.
#
# A per-worker orphan sweep runs on startup to remove stale job folders:
#   - folders whose output.ready is older than ${HOP_ORPHAN_MAX_AGE_MINUTES}
#     (client died before consuming),
#   - folders that have an input.ready but no output.ready and the input.ready is older
#     than that same threshold (worker was killed mid-job and the job is too stale to
#     bother retrying).
#
# Signals: SIGTERM from `docker stop` reaches the inotifywait call, which returns a
# non-zero exit and breaks the while-loop.

set -eu

: "${HOP_JOBS_DIR:?HOP_JOBS_DIR must be set}"
: "${HOP_ORPHAN_MAX_AGE_MINUTES:=1440}"

log() {
    printf '[%s] [hop-worker] %s\n' "$(date -Iseconds)" "$*"
}

mkdir -p "${HOP_JOBS_DIR}"

# Reclaim stale job folders. The client is the normal owner of folder cleanup (it
# deletes the folder after consuming the result), so anything still here past the
# threshold was abandoned.
sweep_orphans() {
    if [ ! -d "${HOP_JOBS_DIR}" ]; then
        return 0
    fi

    find "${HOP_JOBS_DIR}" -mindepth 1 -maxdepth 1 -type d 2>/dev/null \
        | while IFS= read -r folder; do
            # A folder is "active" if it (or any entry inside it) was modified
            # within the last ORPHAN_MAX_AGE_MINUTES minutes.
            # We look for any file/folder more recent (newer) than the threshold.
            # If nothing is newer, the job is stale and we delete.
            recent=$(find "${folder}" -mmin "-${HOP_ORPHAN_MAX_AGE_MINUTES}" \
                        -print -quit 2>/dev/null)
            if [ -z "${recent}" ]; then
                log "orphan-sweep: removing stale job '${folder}'"
                rm -rf "${folder}"
            fi
        done
}

# Process all currently-present <jobId>/input.ready sentinels. Safe to call repeatedly —
# process.sh is idempotent on output.ready: if it already exists, process.sh returns
# early.
scan_once() {
    find "${HOP_JOBS_DIR}" -mindepth 2 -maxdepth 2 -type f -name 'input.ready' -print \
        | while IFS= read -r ready; do
            folder=$(dirname "${ready}")
            /usr/local/bin/process.sh "${folder}" || log "process.sh returned non-zero for '${folder}' (already logged)"
        done
}

log "starting; jobs=${HOP_JOBS_DIR}"
sweep_orphans
scan_once

# Main loop: block on filesystem events, then rescan. We rescan (rather than processing
# the specific file inotifywait reported) because the sentinel is the only event we
# care about — events on payload files are noise. A full pass dispatches strictly on
# input.ready files and ignores the rest.
#
# The -t ${HOP_POLL_FALLBACK_SECONDS:-2} argument forces inotifywait to return
# periodically even when no events fire. That's the safety net for filesystems where
# inotify is unreliable — notably Docker Desktop on Windows (virtiofs/9p bind mounts to
# the host Windows filesystem frequently drop close_write/moved_to events). On Linux
# hosts the events almost always arrive and the scan just finds nothing to do.
while true; do
    inotifywait -q -r -t "${HOP_POLL_FALLBACK_SECONDS:-2}" \
                -e close_write -e moved_to -e create \
                "${HOP_JOBS_DIR}" >/dev/null || true
    scan_once
done
