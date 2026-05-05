#!/bin/sh
# ili2gpkg-worker entrypoint.
#
# Watches ${ILI2GPKG_INPUT_DIR} for new *.input.ready sentinel files and invokes
# process.sh for each one. The sentinel is written last by geopilot, after both the xtf
# and config payloads have been closed, so its appearance is a reliable "this job is
# ready to process" signal that does not depend on rename atomicity (which is unreliable
# on Docker Desktop bind-mounts to the host filesystem on Windows).
#
# Two event sources:
#   1. An initial scan on startup, to pick up sentinels that arrived while the worker was
#      restarting (or were left behind by a previous crashed run).
#   2. inotifywait for close_write + moved_to on the input dir, so a fresh sentinel
#      triggers processing with sub-second latency.
#
# A per-worker orphan sweep runs on startup to remove stale outputs from
# ${ILI2GPKG_OUTPUT_DIR} older than ${ILI2GPKG_ORPHAN_MAX_AGE_MINUTES}. That handles the
# "geopilot died before consuming its output" case.
#
# Signals: tini (PID 1) translates SIGTERM into SIGTERM on this script. `inotifywait`
# blocks the main loop, and SIGTERM on it returns a non-zero exit which breaks the
# while-loop. The currently-running process.sh (if any) gets SIGTERM from tini directly.

set -eu

: "${ILI2GPKG_INPUT_DIR:?ILI2GPKG_INPUT_DIR must be set}"
: "${ILI2GPKG_OUTPUT_DIR:?ILI2GPKG_OUTPUT_DIR must be set}"
: "${ILI2GPKG_ORPHAN_MAX_AGE_MINUTES:=1440}"

log() {
    printf '[%s] [ili2gpkg-worker] %s\n' "$(date --iso-8601=seconds)" "$*"
}

mkdir -p "${ILI2GPKG_INPUT_DIR}" "${ILI2GPKG_OUTPUT_DIR}"

# Delete output files that nobody consumed within the orphan window. Inputs are deleted
# by process.sh on success; orphan inputs (worker died mid-run) are retried on next scan
# because the *.input.ready sentinel is still present.
sweep_orphans() {
    if [ ! -d "${ILI2GPKG_OUTPUT_DIR}" ]; then
        return 0
    fi
    # -mmin takes minutes; find emits errors if the dir is empty, so tolerate that.
    find "${ILI2GPKG_OUTPUT_DIR}" -maxdepth 1 -type f \
        -mmin "+${ILI2GPKG_ORPHAN_MAX_AGE_MINUTES}" -print -delete 2>/dev/null \
        | while IFS= read -r f; do
            log "orphan-sweep: removed stale output '${f}'"
        done
}

# Process all currently-present *.input.ready sentinels. Safe to call repeatedly —
# process.sh is idempotent on the output sentinel: if {basename}.output.ready already
# exists, process.sh returns early.
scan_once() {
    # Use find rather than glob so an empty dir doesn't break the loop. The sentinel is
    # the dispatch trigger; we derive the xtf path from it and hand that to process.sh
    # (which expects an xtf path as $1, same as before).
    find "${ILI2GPKG_INPUT_DIR}" -maxdepth 1 -type f -name '*.input.ready' -print \
        | while IFS= read -r ready; do
            xtf="${ready%.input.ready}.xtf"
            /usr/local/bin/process.sh "${xtf}" || log "process.sh returned non-zero for '${xtf}' (already logged)"
        done
}

log "starting; input=${ILI2GPKG_INPUT_DIR} output=${ILI2GPKG_OUTPUT_DIR}"
sweep_orphans
scan_once

# Main loop: block on filesystem events, then rescan. We rescan (rather than processing
# the specific file inotifywait reported) because the sentinel is the only event we
# care about — events on the xtf or config payloads are noise. A full pass dispatches
# strictly on *.input.ready files and ignores the rest.
#
# The -t ${ILI2GPKG_POLL_FALLBACK_SECONDS:-2} argument forces inotifywait to return
# periodically even when no events fire. That's the safety net for filesystems where
# inotify is unreliable — notably Docker Desktop on Windows (virtiofs/9p bind mounts to
# the host Windows filesystem frequently drop close_write/moved_to events). On Linux
# hosts the events almost always arrive and the scan just finds nothing to do.
while true; do
    inotifywait -q -t "${ILI2GPKG_POLL_FALLBACK_SECONDS:-2}" \
                -e close_write -e moved_to \
                "${ILI2GPKG_INPUT_DIR}" >/dev/null || true
    scan_once
done
