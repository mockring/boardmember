# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BordGameSpace/BordGameSpace.csproj ./BordGameSpace/
RUN dotnet restore ./BordGameSpace/BordGameSpace.csproj

COPY BordGameSpace/ ./BordGameSpace/
RUN dotnet publish ./BordGameSpace/BordGameSpace.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Asia/Taipei
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

# Disable file watchers to prevent inotify exhaustion
ENV DOTNET_CLI_ENABLE_FILE_WATCHING=0
ENV DOTNET_watch=0
ENV ASPNETCORE_HOSTBUILDER__disableFileWatcher=true
ENV ASPNETCORE_HOSTBUILDER__RELOADCONFIGONCHANGE=false

# ASP.NET Core reads ASPNETCORE_URLS to determine port
# When using exec form ENTRYPOINT, this env var is passed directly to dotnet process
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BordGameSpace.dll"]
