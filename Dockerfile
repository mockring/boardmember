# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY BordGameSpace/BordGameSpace.csproj ./BordGameSpace/
RUN dotnet restore ./BordGameSpace/BordGameSpace.csproj

# Copy all source files and build
COPY BordGameSpace/ ./BordGameSpace/
RUN dotnet publish ./BordGameSpace/BordGameSpace.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install timezone data (for Taiwan UTC+8)
RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Set timezone
ENV TZ=Asia/Taipei
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

# Disable file watchers to prevent inotify exhaustion in container
ENV DOTNET_CLI_ENABLE_FILE_WATCHING=0
ENV DOTNET_watch=0
ENV ASPNETCORE_HOSTBUILDER__disableFileWatcher=true
ENV ASPNETCORE_HOSTBUILDER__RELOADCONFIGONCHANGE=false

# ASP.NET Core standard Docker port env var (Render injects PORT env var for this)
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}

COPY --from=build /app/publish .

EXPOSE 10000

# Increase inotify limit to prevent file watcher exhaustion (fallback if /proc is writable)
RUN echo 8192 > /proc/sys/fs/inotify/max_user_instances 2>/dev/null || true

ENTRYPOINT ["dotnet", "BordGameSpace.dll"]
