FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ARG VERSION=0.0.1
ARG REVISION=0000000

# Set default shell
SHELL ["/bin/bash", "-c"]

# Install Node.js
RUN apt-get update && \
  apt-get install -y gnupg && \
  mkdir -p /etc/apt/keyrings && \
  curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg && \
  NODE_MAJOR=20 && \
  echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$NODE_MAJOR.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list && \
  apt-get update && \
  apt-get install nodejs -y && \
  rm -rf /var/lib/apt/lists/*

# Install gosu, a tool to run commands as a specific user
RUN set -eux; \
	apt-get update; \
	apt-get install -y gosu; \
	rm -rf /var/lib/apt/lists/*; \
# verify that the binary works
	gosu nobody true

# Restore dependencies and tools
COPY src/Geopilot.Api/Geopilot.Api.csproj Geopilot.Api/
COPY src/Geopilot.Frontend/nuget.config Geopilot.Frontend/
COPY src/Geopilot.Frontend/package* Geopilot.Frontend/
COPY src/Geopilot.Frontend/Geopilot.Frontend.esproj Geopilot.Frontend/

RUN dotnet restore "Geopilot.Api/Geopilot.Api.csproj"
RUN npm install -C Geopilot.Frontend

# Set environment variables
ENV PUBLISH_DIR=/app/publish
ENV GENERATE_SOURCEMAP=false

# Create optimized production build
COPY src/ .
RUN dotnet publish "Geopilot.Api/Geopilot.Api.csproj" \
  -c Release \
  -p:VersionPrefix=${VERSION} \
  -p:SourceRevisionId=${REVISION} \
  -p:UseAppHost=false \
  -o ${PUBLISH_DIR}

# Generate license and copyright notice for Node packages
COPY config/license.template.json Geopilot.Frontend/
COPY config/license.custom.json ${PUBLISH_DIR}/wwwroot/
RUN \
  cd Geopilot.Frontend && \
  npx license-checker --json --production \
    --customPath license.template.json \
    --out ${PUBLISH_DIR}/wwwroot/license.json

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ENV HOME=/app
ENV TZ=Europe/Zurich
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Storage__UploadDirectory=/uploads
ENV Storage__AssetsDirectory=/assets
ENV PublicAssetsOverride=/public
WORKDIR ${HOME}

# Install missing packages
RUN \
  DEBIAN_FRONTEND=noninteractive && \
  mkdir -p /usr/share/man/man1 /usr/share/man/man2 && \
  apt-get update && \
  apt-get install -y curl sudo vim htop && \
  rm -rf /var/lib/apt/lists/*

# Create directories
RUN \
 mkdir -p $Storage__UploadDirectory && \
 mkdir -p $Storage__AssetsDirectory && \
 mkdir -p $PublicAssetsOverride

EXPOSE 8080
VOLUME $Storage__UploadDirectory
VOLUME $Storage__AssetsDirectory

# Set default locale
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# Copy gosu binaries to the image
COPY --from=build /usr/sbin/gosu /usr/local/bin
COPY --from=build /app/publish $HOME
COPY docker-entrypoint.sh /entrypoint.sh

HEALTHCHECK CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["/entrypoint.sh"]
