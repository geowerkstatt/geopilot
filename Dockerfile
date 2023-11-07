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
COPY src/GeoCop.Frontend/GeoCop.Frontend.esproj GeoCop.Frontend/

RUN dotnet restore "GeoCop.Api/GeoCop.Api.csproj"

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
WORKDIR ${HOME}

EXPOSE 80

# Set default locale
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

COPY --from=build /app/publish $HOME

ENTRYPOINT ["dotnet", "GeoCop.Api.dll"]
