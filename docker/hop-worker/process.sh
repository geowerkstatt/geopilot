#!/bin/sh
# Processes a single Hop job.
#
# Contract:
#   - $1 is an absolute path to a job folder under ${HOP_JOBS_DIR}.
#   - The folder MUST contain args.json describing the pipeline and parameters, plus the
#     payload file(s) the operation needs.
#     The client drops a zero-byte ${folder}/input.ready sentinel last; that is what
#     entrypoint.sh dispatches on.
#   - On success the worker writes the operation's result file(s) in the SAME folder,
#     then writes a zero-byte ${folder}/output.ready sentinel last.
#   - The worker ALWAYS writes a log file capturing the entire Hop output (stdout +
#     stderr) plus any worker-side diagnostic. On success it is named
#     ${folder}/success.log; on failure it is named ${folder}/error.log. The client can
#     read either to surface diagnostics. On failure, error.log is the source of truth
#     for the failure message.
#   - The client should cleanup the job folder after it has consumed all results.
#     If the client does not cleanup the job folder, the orphan-sweep in entrypoint.sh
#     will clean it up after the configured period of time.
#
# args.json shape:
#
#   {
#     "pipeline": "/shared/hop/pipelines/transform_xtf.hpl",
#     "parameters": {
#       "SOURCE_SRS": "2056",
#       "TARGET_FORMAT": "gpkg"
#     }
#   }

set -eu

hop_run="/opt/hop/hop-run.sh"

job_dir="$1"
job_id=$(basename "${job_dir}")
args_file="${job_dir}/args.json"
input_ready="${job_dir}/input.ready"
output_ready="${job_dir}/output.ready"
success_log="${job_dir}/success.log"
error_log="${job_dir}/error.log"

log() {
    printf '[%s] [hop-worker/%s] %s\n' "$(date -Iseconds)" "${job_id}" "$*"
}

fail() {
    msg="$1"
    log "${msg}"
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

# Parse args.json.
pipeline=$(jq -r '.pipeline // empty' < "${args_file}")
parameters=$(jq -r '.parameters // {} | to_entries | map(.key + "=" + .value) | join(",")' < "${args_file}")

if [ -z "${pipeline}" ]; then
    fail "args.json is missing required field 'pipeline'"
fi

log "starting (pipeline=${pipeline})"

# cd into the job folder so bare filenames resolve against the cwd.
cd "${job_dir}"

set -- "${hop_run}" --file="${pipeline}" --runconfig=local
if [ -n "${parameters}" ]; then
    set -- "$@" "--parameters=${parameters}"
fi
set -- "$@" "--parameters=inputDir=${job_dir}/input,outputDir=${job_dir}/output"

echo "$@" >> "${success_log}"

set +e
"$@" >> "${success_log}" 2>&1
exit_code=$?
set -e

if [ "${exit_code}" -ne 0 ]; then
    fail "${hop_run} failed (exit_code=${exit_code})"
fi

: > "${output_ready}"

log "completed (exit_code=0)"
exit 0
