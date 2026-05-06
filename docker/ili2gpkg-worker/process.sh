#!/bin/sh
# Processes a single ili2gpkg job.
#
# Contract:
#   - $1 is an absolute path to a job folder under ${ILI2GPKG_JOBS_DIR}.
#   - The folder MUST contain args.json describing the operation and flags, plus the
#     payload file(s) the operation needs under fixed names defined by the protocol:
#         model.ili     INTERLIS model (input for schemaimport)
#         data.xtf      XTF data (input for import; output for export)
#         dbfile.gpkg   GeoPackage (output for schemaimport; input/output for import,
#                       written in place; input for export)
#     The client drops a zero-byte ${folder}/input.ready sentinel last; that is what
#     entrypoint.sh dispatches on.
#   - On success the worker writes the operation's result file (dbfile.gpkg for
#     schemaimport/import, data.xtf for export) under its fixed name in the SAME
#     folder, then writes a zero-byte ${folder}/output.ready sentinel last.
#   - The worker ALWAYS writes a log file capturing the entire ili2gpkg output (stdout +
#     stderr) plus any worker-side diagnostic. On success it is named
#     ${folder}/success.log; on failure it is named ${folder}/error.log. The client can
#     read either to surface diagnostics. On failure, error.log is the source of truth
#     for the failure message — there is no separate "error" file.
#   - The client should cleanup the job folder after it has consumed all results.
#     If the client does not cleanup the job folder, the orphan-sweep in entrypoint.sh
#     will clean it up after the configured period of time.
#
# args.json shape (field names match ili2gpkg flags exactly, plus "operation"):
#
#   {
#     "operation":          "schemaimport" | "import" | "export",
#     "models":             ["MyModel"],         // optional, joined with ';'
#     "disableValidation":  false,
#     "createBasketCol":    false,
#     "defaultSrsCode":     2056
#   }

set -eu

: "${ILI2GPKG_HOME:?ILI2GPKG_HOME must be set (inherited from ili2gpkg image)}"
: "${ILI2GPKG_VERSION:?ILI2GPKG_VERSION must be set (inherited from ili2gpkg image)}"
: "${ILI2GPKG_CACHE_DIR:=/var/cache/ili2gpkg}"

# Fixed filenames defined by the protocol.
MODEL_FILE="model.ili"
DATA_FILE="data.xtf"
DBFILE="dbfile.gpkg"

job_dir="$1"
job_id=$(basename "${job_dir}")
args_file="${job_dir}/args.json"
input_ready="${job_dir}/input.ready"
output_ready="${job_dir}/output.ready"
success_log="${job_dir}/success.log"
error_log="${job_dir}/error.log"

log() {
    printf '[%s] [ili2gpkg-worker/%s] %s\n' "$(date --iso-8601=seconds)" "${job_id}" "$*"
}

fail() {
    msg="$1"
    log "${msg}"
    # If ili2gpkg already wrote success.log, repurpose it as error.log so its output is retained.
    if [ -e "${success_log}" ] && [ ! -e "${error_log}" ]; then
        mv "${success_log}" "${error_log}"
    fi
    printf '%s\n' "${msg}" >> "${error_log}"
    : > "${output_ready}"
    exit 1
}

# Skip if already processed.
if [ -e "${output_ready}" ]; then
    exit 0
fi

# args.json may briefly lag the input sentinel on flaky bind-mounts; retry once.
if [ ! -r "${args_file}" ]; then
    log "waiting 1s for args.json"
    sleep 1
fi
if [ ! -r "${args_file}" ]; then
    fail "args.json not found in job folder"
fi

# Parse args.json. jq -r yields literal "null" for missing keys, so guard before use.
operation=$(jq -r '.operation // empty' < "${args_file}")
models=$(jq -r '.models // [] | join(";")' < "${args_file}")
disable_validation=$(jq -r '.disableValidation // false' < "${args_file}")
create_basket_col=$(jq -r '.createBasketCol // false' < "${args_file}")
default_srs_code=$(jq -r '.defaultSrsCode // empty' < "${args_file}")

# Build ili2gpkg argv based on operation.
case "${operation}" in
    schemaimport)
        set -- --schemaimport --dbfile "${DBFILE}"
        subject="${MODEL_FILE}"
        ;;
    import)
        set -- --import --dbfile "${DBFILE}"
        subject="${DATA_FILE}"
        ;;
    export)
        set -- --export --dbfile "${DBFILE}"
        subject="${DATA_FILE}"
        ;;
    *)
        fail "unknown operation '${operation}' (expected: schemaimport, import, export)"
        ;;
esac

if [ "${create_basket_col}" = "true" ]; then set -- "$@" --createBasketCol; fi
if [ "${disable_validation}" = "true" ]; then set -- "$@" --disableValidation; fi
if [ -n "${models}" ]; then set -- "$@" --models "${models}"; fi
if [ -n "${default_srs_code}" ]; then set -- "$@" --defaultSrsCode "${default_srs_code}"; fi

log "starting (operation=${operation})"

mkdir -p "${ILI2GPKG_CACHE_DIR}"
# ili2gpkg resolves --ilicache via HOME; point it at the persistent cache volume.
export HOME="${ILI2GPKG_CACHE_DIR}"

# cd into the job folder so bare filenames in argv resolve against the cwd.
cd "${job_dir}"

# Log java command
echo "java -jar ${ILI2GPKG_HOME}/ili2gpkg-${ILI2GPKG_VERSION}.jar $* ${subject}" >> "${success_log}"

# Run ili2gpkg and capture output to success.log; fail() renames it to error.log on failure.
set +e
java -jar "${ILI2GPKG_HOME}/ili2gpkg-${ILI2GPKG_VERSION}.jar" "$@" "${subject}" \
    >> "${success_log}" 2>&1
exit_code=$?
set -e

if [ "${exit_code}" -ne 0 ]; then
    fail "ili2gpkg failed (exit_code=${exit_code})"
fi

# Write output.ready sentinel
: > "${output_ready}"

log "completed (exit_code=0)"
exit 0
