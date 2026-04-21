FROM eclipse-temurin:17-jre-jammy

ARG ILI2GPKG_VERSION=5.1.0
ENV ILI2GPKG_VERSION=${ILI2GPKG_VERSION}
ENV ILI2GPKG_HOME=/opt/ili2gpkg

# Download the ili2gpkg release zip from the official Eisenhut distribution mirror.
# The zip contains the main JAR plus a libs/ directory referenced from the manifest's Class-Path,
# so the whole archive must be extracted into a single directory and run with the working
# directory set there.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates unzip \
    && mkdir -p "${ILI2GPKG_HOME}" \
    && curl -fsSL -o /tmp/ili2gpkg.zip \
        "https://downloads.interlis.ch/ili2gpkg/ili2gpkg-${ILI2GPKG_VERSION}.zip" \
    && unzip -q /tmp/ili2gpkg.zip -d "${ILI2GPKG_HOME}" \
    && rm /tmp/ili2gpkg.zip \
    && apt-get purge -y curl unzip \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

LABEL org.opencontainers.image.title="ili2gpkg" \
      org.opencontainers.image.description="CLI tool for converting between INTERLIS transfer files (XTF/ITF) and GeoPackage. Built from the upstream release at downloads.interlis.ch." \
      org.opencontainers.image.source="https://github.com/GeoWerkstatt/geopilot" \
      org.opencontainers.image.licenses="LGPL-2.1-only"

WORKDIR /data

# Resolve the JAR path at container start time so the ${ILI2GPKG_VERSION} var is picked up.
ENTRYPOINT ["sh", "-c", "exec java -jar ${ILI2GPKG_HOME}/ili2gpkg-${ILI2GPKG_VERSION}.jar \"$@\"", "--"]
