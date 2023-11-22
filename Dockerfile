FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
ARG VERSION
ARG REVISION

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

# Restore dependencies and tools
COPY src/GeoCop.Api/GeoCop.Api.csproj GeoCop.Api/
COPY src/GeoCop.Frontend/nuget.config GeoCop.Frontend/
COPY src/GeoCop.Frontend/package* GeoCop.Frontend/
COPY src/GeoCop.Frontend/GeoCop.Frontend.esproj GeoCop.Frontend/

RUN dotnet restore "GeoCop.Api/GeoCop.Api.csproj"
RUN npm install -C GeoCop.Frontend

# Set environment variables
ENV PUBLISH_DIR=/app/publish
ENV GENERATE_SOURCEMAP=false

# Create optimized production build
COPY src/ .
RUN dotnet publish "GeoCop.Api/GeoCop.Api.csproj" \
  -c Release \
  -p:VersionPrefix=${VERSION} \
  -p:SourceRevisionId=${REVISION} \
  -p:UseAppHost=false \
  -o ${PUBLISH_DIR}

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
ENV HOME=/app
ENV TZ=Europe/Zurich
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Storage__UploadDirectory=/uploads
WORKDIR ${HOME}

# Install missing packages
RUN \
  DEBIAN_FRONTEND=noninteractive && \
  mkdir -p /usr/share/man/man1 /usr/share/man/man2 && \
  apt-get update && \
  apt-get install -y sudo vim htop libcap2-bin && \
  rm -rf /var/lib/apt/lists/*

# Add non-root user
RUN \
 useradd --uid 941 --user-group --home $HOME --shell /bin/bash abc && \
 usermod --groups users abc && \
 mkdir -p $Storage__UploadDirectory

# Install temp CA and SSL cert so that the SSL port can be exposed.
# The cert is self-signed and only valid for localhost. Because a valid and dynamic
# SSL cert is used for hosting, the cert is only used to expose the SSL port.
ENV Kestrel:Certificates:Default:Path=/etc/ssl/private/cert.pfx
ENV Kestrel:Certificates:Default:Password=changeit
ENV Kestrel:Certificates:Default:AllowInvalid=true
ENV Kestrel:EndPointDefaults:Protocols=Http1AndHttp2

RUN apt-get update && apt-get install curl -y && \
	curl -L https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64 > /usr/local/bin/mkcert && \
	chmod +x /usr/local/bin/mkcert
RUN mkcert -install
RUN mkcert -p12-file /etc/ssl/private/cert.pfx -pkcs12 localhost 127.0.0.1 ::1

EXPOSE 443
VOLUME $Storage__UploadDirectory

# Set default locale
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# Allow dotnet to bind to well known ports
RUN setcap CAP_NET_BIND_SERVICE=+eip /usr/share/dotnet/dotnet

COPY --from=build /app/publish $HOME
COPY docker-entrypoint.sh /entrypoint.sh

HEALTHCHECK CMD curl --fail http://localhost/health || exit 1

ENTRYPOINT ["/entrypoint.sh"]
