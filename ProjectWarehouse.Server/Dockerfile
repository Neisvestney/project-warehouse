FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
# Data Protection key ring: the mounted volume is root-owned otherwise and the app cannot write it
RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update \
    && apt-get install -y curl \
    && curl -fsSL https://deb.nodesource.com/setup_25.x | bash - \
    && apt-get install -y nodejs
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ProjectWarehouse.Server/ProjectWarehouse.Server.csproj", "ProjectWarehouse.Server/"]
COPY ["projectwarehouse.client/projectwarehouse.client.esproj", "projectwarehouse.client/"]
RUN dotnet restore "ProjectWarehouse.Server/ProjectWarehouse.Server.csproj"
COPY ["projectwarehouse.client/package.json", "projectwarehouse.client/"]
WORKDIR /src/projectwarehouse.client
RUN npm install --no-audit --no-fund --verbose
WORKDIR /src
COPY . .
WORKDIR "/src/ProjectWarehouse.Server"
RUN dotnet build "./ProjectWarehouse.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
RUN mkdir -p ~/.aspnet/https
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProjectWarehouse.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProjectWarehouse.Server.dll"]
