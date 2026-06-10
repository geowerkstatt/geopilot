#!/bin/sh
# Processes a single XTF-Diff-Tool job.
#
# Contract:
#   - $1 is an absolute path to a job folder under ${XTF_DIFF_JOBS_DIR}.
#   - The folder MUST contain the two XTF files to compare under fixed names defined
#     by the protocol:
#         old.xtf       the previous state of the data
#         new.xtf       the current state of the data
#     plus an optional args.json describing tool flags. The client drops a zero-byte
#     ${folder}/input.ready sentinel last; that is what entrypoint.sh dispatches on.
#   - On success the worker writes the diff result under its fixed name diff.json in
#     the SAME folder, then writes a zero-byte ${folder}/output.ready sentinel last.
#   - The worker ALWAYS writes a log file capturing the entire XTF-Diff-Tool output
#     (stdout + stderr) plus any worker-side diagnostic. On success it is named
#     ${folder}/success.log; on failure it is named ${folder}/error.log. The client can
#     read either to surface diagnostics. On failure, error.log is the source of truth
#     for the failure message — there is no separate "error" file.
#   - The client should cleanup the job folder after it has consumed all results.
#     If the client does not cleanup the job folder, the orphan-sweep in entrypoint.sh
#     will clean it up after the configured period of time.
#
# args.json shape (field names match XTF-Diff-Tool flags exactly):
#
#   {
#     "modeldir": "https://models.interlis.ch/"    // optional, passed as --modeldir
#   }

set -eu

: "${XTF_DIFF_TOOL_BIN:?XTF_DIFF_TOOL_BIN must be set (inherited from xtf-diff-tool image)}"
: "${XTF_DIFF_CACHE_DIR:=/var/cache/xtf-diff-tool}"

# Fixed filenames defined by the protocol.
OLD_FILE="old.xtf"
NEW_FILE="new.xtf"
DIFF_FILE="diff.json"

job_dir="$1"
job_id=$(basename "${job_dir}")
args_file="${job_dir}/args.json"
output_ready="${job_dir}/output.ready"
success_log="${job_dir}/success.log"
error_log="${job_dir}/error.log"

log() {
    printf '[%s] [xtf-diff-worker/%s] %s\n' "$(date --iso-8601=seconds)" "${job_id}" "$*"
}

fail() {
    msg="$1"
    log "${msg}"
    # If the tool already wrote success.log, repurpose it as error.log so its output is retained.
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

if [ ! -r "${job_dir}/${OLD_FILE}" ] || [ ! -r "${job_dir}/${NEW_FILE}" ]; then
    fail "old.xtf and new.xtf are both required in the job folder"
fi

# Parse the optional args.json. jq -r yields literal "null" for missing keys, so guard before use.
modeldir=""
if [ -r "${args_file}" ]; then
    modeldir=$(jq -r '.modeldir // empty' < "${args_file}")
fi

# Build XTF-Diff-Tool argv.
set --
if [ -n "${modeldir}" ]; then set -- "$@" --modeldir "${modeldir}"; fi

log "starting"

mkdir -p "${XTF_DIFF_CACHE_DIR}"
# The tool resolves its INTERLIS model cache via HOME; point it at the persistent cache volume.
export HOME="${XTF_DIFF_CACHE_DIR}"

# cd into the job folder so bare filenames in argv resolve against the cwd.
cd "${job_dir}"

# Log the command
tool_cmd="${XTF_DIFF_TOOL_BIN} $* ${OLD_FILE} ${NEW_FILE} ${DIFF_FILE}"
echo "${tool_cmd}" >> "${success_log}"
log "${tool_cmd}"

# Run the XTF-Diff-Tool and capture output to success.log; fail() renames it to error.log on failure.
set +e
"${XTF_DIFF_TOOL_BIN}" "$@" "${OLD_FILE}" "${NEW_FILE}" "${DIFF_FILE}" \
    >> "${success_log}" 2>&1
exit_code=$?
set -e

if [ "${exit_code}" -ne 0 ]; then
    fail "XTF-Diff-Tool failed (exit_code=${exit_code})"
fi

if [ ! -r "${DIFF_FILE}" ]; then
    fail "XTF-Diff-Tool exited successfully but produced no ${DIFF_FILE}"
fi

# Write output.ready sentinel
: > "${output_ready}"

log "completed (exit_code=0)"
exit 0
