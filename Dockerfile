# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY BordGameSpace/BordGameSpace.csproj ./BordGameSpace/
RUN dotnet restore ./BordGameSpace/BordGameSpace.csproj

# Copy all source files and build
COPY BordGameSpace/ ./BordGameSpace/
RUN dotnet publish ./BordGameSpace/BordGameSpace.csproj -c Release -o /app/publish --no-restore

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

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "BordGameSpace.dll", "--server.urls", "http://0.0.0.0:8080"]
