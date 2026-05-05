#!/bin/sh
# Processes a single XTF file via ili2gpkg.
#
# Contract:
#   - $1 is an absolute path to an XTF file under ${ILI2GPKG_INPUT_DIR}.
#   - A sibling file with the same basename and ".config.json" extension MUST exist.
#     It supplies the ili2gpkg flags (models, defaultSrsCode, etc.) for this specific
#     conversion, so pipelines can vary flags per delivery.
#   - A sibling ".input.ready" sentinel (written last by geopilot) signals that both the
#     xtf and config payloads are fully written. Its existence is what entrypoint.sh
#     dispatches on; this script just trusts the basename it was given.
#   - Output filename is derived from the input basename (Guid without ".xtf" extension),
#     producing <basename>.gpkg on success or <basename>.error on failure in
#     ${ILI2GPKG_OUTPUT_DIR}, then a zero-byte <basename>.output.ready sentinel last.
#     Geopilot polls only for the sentinel — visibility of the .gpkg or .error alone is
#     not a signal of completion. This decouples the contract from rename atomicity,
#     which is unreliable on bind-mounted host filesystems (Docker Desktop on Windows).
#   - Inputs (xtf + config + input.ready) are deleted on success. On failure they are
#     kept for diagnostics; the orphan sweep eventually reclaims them.

set -eu

: "${ILI2GPKG_HOME:?ILI2GPKG_HOME must be set (inherited from ili2gpkg image)}"
: "${ILI2GPKG_VERSION:?ILI2GPKG_VERSION must be set (inherited from ili2gpkg image)}"
: "${ILI2GPKG_OUTPUT_DIR:?ILI2GPKG_OUTPUT_DIR must be set}"
: "${ILI2GPKG_CACHE_DIR:=/var/cache/ili2gpkg}"

xtf="$1"
basename=$(basename "${xtf}" .xtf)
config="$(dirname "${xtf}")/${basename}.config.json"
input_ready="$(dirname "${xtf}")/${basename}.input.ready"
out_ok="${ILI2GPKG_OUTPUT_DIR}/${basename}.gpkg"
out_err="${ILI2GPKG_OUTPUT_DIR}/${basename}.error"
out_ready="${ILI2GPKG_OUTPUT_DIR}/${basename}.output.ready"

log() {
    printf '[%s] [ili2gpkg-worker/%s] %s\n' "$(date --iso-8601=seconds)" "${basename}" "$*"
}

fail() {
    msg="$1"
    log "${msg}"
    printf '%s\n' "${msg}" > "${out_err}"
    : > "${out_ready}"
    # Keep inputs so the operator can inspect; orphan-sweep will reclaim them.
    exit 1
}

# Skip if already processed. Sentinel presence is the source of truth — a stale
# half-written .gpkg without a sentinel is treated as "not done" and reprocessed.
# Also reclaim any inputs that survived a crash between sentinel-write and input
# deletion: scan_once will keep finding the input.ready until we delete it.
if [ -e "${out_ready}" ]; then
    log "skipping: output sentinel already present; reclaiming orphan inputs"
    rm -f "${xtf}" "${config}" "${input_ready}"
    exit 0
fi

# Belt-and-braces visibility checks. The input sentinel was written last by geopilot
# and triggers entrypoint.sh's scan, but on a flaky bind-mount the xtf or config may
# briefly lag. A 1s retry covers that; longer lag means something is actually wrong.
if [ ! -r "${xtf}" ]; then
    log "waiting 1s for xtf payload"
    sleep 1
fi
if [ ! -r "${xtf}" ]; then
    fail "xtf payload not found: ${xtf}"
fi
if [ ! -r "${config}" ]; then
    log "waiting 1s for config sidecar"
    sleep 1
fi
if [ ! -r "${config}" ]; then
    fail "config sidecar not found: ${config}"
fi

# Parse the config. Using jq -r means missing keys yield literal "null" — guard each
# read before using it.
original_name=$(jq -r '.originalFileName // empty' < "${config}")
do_import=$(jq -r '.import // true' < "${config}")
do_disable_validation=$(jq -r '.disableValidation // false' < "${config}")
do_create_basket_col=$(jq -r '.createBasketCol // false' < "${config}")
do_smart2=$(jq -r '.smart2Inheritance // false' < "${config}")
do_schema_import=$(jq -r '.schemaImport // false' < "${config}")
models=$(jq -r '.models // [] | join(";")' < "${config}")
srs_code=$(jq -r '.defaultSrsCode // empty' < "${config}")

log "starting conversion (original=${original_name:-'<unnamed>'})"

# Build ili2gpkg argv piece by piece. We point --dbfile straight at the final output
# name; partial writes from a crashed ili2gpkg run are harmless because geopilot waits
# for ${out_ready} before consuming, and this script clears any stale ${out_ok} below
# before re-running.
rm -f "${out_ok}"
set -- --dbfile "${out_ok}"
if [ "${do_import}" = "true" ]; then set -- "$@" --import; fi
if [ "${do_schema_import}" = "true" ]; then set -- "$@" --schemaimport; fi
if [ "${do_create_basket_col}" = "true" ]; then set -- "$@" --createBasketCol; fi
if [ "${do_smart2}" = "true" ]; then set -- "$@" --smart2Inheritance; fi
if [ "${do_disable_validation}" = "true" ]; then set -- "$@" --disableValidation; fi
if [ -n "${models}" ]; then set -- "$@" --models "${models}"; fi
if [ -n "${srs_code}" ]; then set -- "$@" --defaultSrsCode "${srs_code}"; fi

mkdir -p "${ILI2GPKG_CACHE_DIR}"
# ili2gpkg resolves the `--ilicache` at runtime; we set HOME so it places its cache
# under the persistent volume mount regardless of process user.
export HOME="${ILI2GPKG_CACHE_DIR}"

# ili2gpkg's own working directory must be the unpacked release dir because the JAR
# manifest's Class-Path points at `libs/...` relative to that dir.
cd "${ILI2GPKG_HOME}"

# Capture combined output; non-zero exit code with a produced gpkg means "warnings",
# which ili2gpkg distinguishes from "failed" by whether the gpkg exists.
set +e
java -jar "${ILI2GPKG_HOME}/ili2gpkg-${ILI2GPKG_VERSION}.jar" "$@" "${xtf}" 2>&1
rc=$?
set -e

if [ -f "${out_ok}" ]; then
    # Sentinel last — only after this line is geopilot allowed to consume the gpkg.
    : > "${out_ready}"
    rm -f "${xtf}" "${config}" "${input_ready}"
    if [ "${rc}" -ne 0 ]; then
        log "conversion completed with warnings (rc=${rc})"
    else
        log "conversion completed (rc=0)"
    fi
    exit 0
else
    # No output produced. fail() emits the .error payload then the sentinel.
    fail "ili2gpkg failed (rc=${rc}) and produced no output file"
fi
