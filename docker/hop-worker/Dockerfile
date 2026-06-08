# syntax=docker.io/docker/dockerfile:1
# check=error=true

# Long-running worker that watches a shared jobs directory for per-request job folders
# dropped by any client, runs Apache Hop over each, and writes the result file plus a
# per-job log back into the same folder. Self-contained: clients drive it purely through
# the shared filesystem, no socket, no SDK.

FROM apache/hop

ARG GIS_PLUGIN_VERSION=1.4.0

# The worker reads and writes per-job folders under this single shared root. It must be
# bind-mounted into the container by whatever orchestrator is running it; the path is
# fixed inside the image so the entrypoint script can assume it.
ENV HOP_JOBS_DIR=/shared/hop/jobs \
    HOP_ORPHAN_MAX_AGE_MINUTES=1440

RUN wget -O /tmp/gis-plugin-assemblies.zip https://github.com/atolcd/hop-gis-plugins/releases/download/v${GIS_PLUGIN_VERSION}/gis-plugin-assemblies-${GIS_PLUGIN_VERSION}.zip \
    && unzip -q /tmp/gis-plugin-assemblies.zip -d /opt/hop/plugins/ \
    && rm /tmp/gis-plugin-assemblies.zip

RUN wget -O /tmp/geometryfieldsconverter.zip https://gitlab.ost.ch/apache-hop-plugin-sa/apache-hop-plugins-geometry-fields-converter/-/jobs/1481085/artifacts/raw/geometryfieldsconverter.zip \
    && mkdir -p /opt/hop/plugins/transforms/geometryfieldsconverter/ \
    && unzip -q /tmp/geometryfieldsconverter.zip -d /opt/hop/plugins/transforms/geometryfieldsconverter/ \
    && rm /tmp/geometryfieldsconverter.zip

USER root
RUN apk add --no-cache inotify-tools jq tini
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
COPY process.sh /usr/local/bin/process.sh
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/process.sh

USER hop

LABEL org.opencontainers.image.title="hop-worker" \
      org.opencontainers.image.description="Long-running worker that runs Apache Hop pipelines over per-job folders dropped into a shared directory. Self-contained - driven purely through the shared filesystem." \
      org.opencontainers.image.source="https://github.com/GeoWerkstatt/geopilot" \
      org.opencontainers.image.licenses="LGPL-2.1-only"

# tini forwards signals and reaps zombies so SIGTERM from `docker stop` actually reaches
# the entrypoint loop and any child process.sh invocations.
ENTRYPOINT ["/sbin/tini", "--", "/usr/local/bin/entrypoint.sh"]
