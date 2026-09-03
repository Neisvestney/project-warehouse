# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*
# Data Protection key ring and file storage: the mounted volumes are root-owned otherwise and the app cannot write them
RUN mkdir -p /keys /data/files && chown -R $APP_UID:$APP_UID /keys /data/files
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
# vite build runs during publish, so the SDK stage needs node; copied from the official image
# instead of the NodeSource install script — no extra apt round trip and no network script to break.
# Runtime only: no include/node headers, so a native dependency needing node-gyp would not build here
COPY --from=node:25-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:25-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx
ARG BUILD_CONFIGURATION=Release
# Release tag, stamped into service.version on both halves of the telemetry. Empty falls back
# to the git hash on the client and to the csproj version on the server.
ARG APP_VERSION=
ENV VITE_APP_VERSION=${APP_VERSION}
WORKDIR /src
COPY ["ProjectWarehouse.Server/ProjectWarehouse.Server.csproj", "ProjectWarehouse.Server/"]
COPY ["projectwarehouse.client/projectwarehouse.client.esproj", "projectwarehouse.client/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "ProjectWarehouse.Server/ProjectWarehouse.Server.csproj"
# npm layers come after the NuGet ones: client dependencies churn more often, so they invalidate less
COPY ["projectwarehouse.client/package.json", "projectwarehouse.client/package-lock.json", "projectwarehouse.client/"]
WORKDIR /src/projectwarehouse.client
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund
WORKDIR /src
COPY . .
WORKDIR "/src/ProjectWarehouse.Server"
# vite.config.ts exports a dev certificate while loading, even for a production build,
# and dotnet dev-certs refuses to create the target directory itself
RUN mkdir -p ~/.aspnet/https
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "./ProjectWarehouse.Server.csproj" -c $BUILD_CONFIGURATION --no-restore \
    -o /app/publish /p:UseAppHost=false \
    ${APP_VERSION:+/p:Version=$APP_VERSION}

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProjectWarehouse.Server.dll"]
