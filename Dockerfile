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
ENV DOTNET_CLI_ENABLE_FILE_WATCHING=0
ENV ASPNETCORE_hostBuilder__disableFileWatcher=true
ENV DOTNET_watch=0
ENV ASPNETCORE_HOSTBUILDER__RELOADCONFIGONCHANGE=false

COPY --from=build /app/publish .

EXPOSE 10000

# Increase inotify limit to prevent file watcher exhaustion crashes
RUN echo 8192 > /proc/sys/fs/inotify/max_user_instances || true

ENTRYPOINT dotnet BordGameSpace.dll --server.urls http://0.0.0.0:$${PORT:-10000}
