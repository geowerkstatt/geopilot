# syntax=docker.io/docker/dockerfile:1
# check=error=true

# Long-running worker that watches a shared jobs directory for per-request job folders
# dropped by any client, runs the XTF-Diff-Tool over each (comparing two XTF files), and
# writes the resulting diff JSON plus a per-job log back into the same folder.
# Self-contained: clients drive it purely through the shared filesystem, no socket, no SDK.

ARG XTF_DIFF_TOOL_VERSION=v1.0.19

# The official XTF-Diff-Tool image (https://github.com/geowerkstatt/XTF-Diff-Tool) ships the
# CLI as a Gradle installDist layout under /app with the launcher script /app/bin/XTF-Diff-Tool
# and runs on eclipse-temurin:25-jre. The worker reuses it as base and only adds the watch loop:
#   - inotify-tools: inotifywait for the watch loop.
#   - jq:            parse the per-job args.json sidecar.
#   - tini:          PID 1 signal handler so SIGTERM from `docker stop` reaches the loop.
FROM ghcr.io/geowerkstatt/xtf-diff-tool:${XTF_DIFF_TOOL_VERSION}

ARG XTF_DIFF_TOOL_VERSION
ENV XTF_DIFF_TOOL_VERSION=${XTF_DIFF_TOOL_VERSION} \
    XTF_DIFF_TOOL_BIN=/app/bin/XTF-Diff-Tool

# The base image runs as the non-root user "app"; package installation and the worker loop
# need root (the shared bind mount receives job folders created by arbitrary client uids).
USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends inotify-tools jq tini \
    && rm -rf /var/lib/apt/lists/*

# The worker reads and writes per-job folders under this single shared root. It must be
# bind-mounted into the container by whatever orchestrator is running it; the path is
# fixed inside the image so the entrypoint script can assume it.
ENV XTF_DIFF_JOBS_DIR=/shared/xtf-diff/jobs \
    XTF_DIFF_CACHE_DIR=/var/cache/xtf-diff-tool \
    XTF_DIFF_ORPHAN_MAX_AGE_MINUTES=1440

# Cache dir is a named volume target by convention; persisting it across restarts avoids
# re-downloading INTERLIS models from the model repositories on every worker recycle.
VOLUME ${XTF_DIFF_CACHE_DIR}

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY process.sh /usr/local/bin/process.sh
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/process.sh

LABEL org.opencontainers.image.title="xtf-diff-worker" \
      org.opencontainers.image.description="Long-running worker that runs the XTF-Diff-Tool over per-job folders dropped into a shared directory. Self-contained — driven purely through the shared filesystem." \
      org.opencontainers.image.source="https://github.com/GeoWerkstatt/geopilot" \
      org.opencontainers.image.licenses="AGPL-3.0-only"

# tini forwards signals and reaps zombies so SIGTERM from `docker stop` actually reaches
# the entrypoint loop and any child process.sh invocations.
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
