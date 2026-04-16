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
ENV DOTNET_CLI_ENABLE_FILE_WATCHING=0
ENV DOTNET_watch=0
ENV ASPNETCORE_HOSTBUILDER__disableFileWatcher=true
ENV ASPNETCORE_HOSTBUILDER__RELOADCONFIGONCHANGE=false

# Hardcode 10000 as the port - no env var dependency
# Shell form so $PORT can be expanded by Render if they inject it
# Default to 10000 (Render's expected port), override with $PORT if set
ENV PORT=10000

COPY --from=build /app/publish .

EXPOSE 10000

ENTRYPOINT dotnet BordGameSpace.dll --server.urls http://0.0.0.0:$PORT
