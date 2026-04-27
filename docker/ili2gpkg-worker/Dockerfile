# syntax=docker.io/docker/dockerfile:1
# check=error=true

# Long-running worker that watches a shared input directory for XTF files dropped by
# geopilot, converts each one via ili2gpkg, and writes the resulting GeoPackage to a
# shared output directory. Replaces the previous pattern of having geopilot spawn ili2gpkg
# containers itself (which required mounting the docker socket into geopilot).

FROM eclipse-temurin:17-jre-jammy

ARG ILI2GPKG_VERSION=5.1.0
ENV ILI2GPKG_VERSION=${ILI2GPKG_VERSION} \
    ILI2GPKG_HOME=/opt/ili2gpkg

# Install both the ili2gpkg runtime and the worker-specific tooling in one layer.
#   - curl, ca-certificates, unzip: used only to fetch and extract the ili2gpkg release,
#     purged at the end of the same layer to keep the image small.
#   - inotify-tools: inotifywait for the watch loop.
#   - jq:            parse the per-file config sidecar (<basename>.config.json).
#   - tini:          PID 1 signal handler so SIGTERM from `docker stop` reaches the loop.
#
# The ili2gpkg zip contains the main JAR plus a libs/ directory referenced from the JAR
# manifest's Class-Path, so the whole archive must be extracted into a single directory
# and ili2gpkg must be invoked with the working directory set there.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl ca-certificates unzip \
        inotify-tools jq tini \
    && mkdir -p "${ILI2GPKG_HOME}" \
    && curl -fsSL -o /tmp/ili2gpkg.zip \
        "https://downloads.interlis.ch/ili2gpkg/ili2gpkg-${ILI2GPKG_VERSION}.zip" \
    && unzip -q /tmp/ili2gpkg.zip -d "${ILI2GPKG_HOME}" \
    && rm /tmp/ili2gpkg.zip \
    && apt-get purge -y curl unzip \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

# The worker writes to these paths; they must be bind-mounted into the container by
# whatever orchestrator is running it. The paths are fixed inside the image so the
# entrypoint script can assume them.
ENV ILI2GPKG_INPUT_DIR=/shared/ili2gpkg/in \
    ILI2GPKG_OUTPUT_DIR=/shared/ili2gpkg/out \
    ILI2GPKG_CACHE_DIR=/var/cache/ili2gpkg \
    ILI2GPKG_ORPHAN_MAX_AGE_MINUTES=1440

# Cache dir is a named volume target by convention; persisting it across restarts avoids
# re-downloading INTERLIS models from models.interlis.ch on every worker recycle.
VOLUME ${ILI2GPKG_CACHE_DIR}

COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY process.sh /usr/local/bin/process.sh
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/process.sh

LABEL org.opencontainers.image.title="ili2gpkg-worker" \
      org.opencontainers.image.description="Long-running worker that converts XTF files dropped into a shared input directory by GeoPilot into GeoPackage files." \
      org.opencontainers.image.source="https://github.com/GeoWerkstatt/geopilot" \
      org.opencontainers.image.licenses="LGPL-2.1-only"

# tini forwards signals and reaps zombies so SIGTERM from `docker stop` actually reaches
# the entrypoint loop and any child process.sh invocations.
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint.sh"]
