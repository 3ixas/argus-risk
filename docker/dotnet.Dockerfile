# Multi-target Dockerfile for all Argus .NET services
# Usage: docker build --build-arg SERVICE_NAME=Argus.Api -f docker/dotnet.Dockerfile .

ARG SERVICE_NAME

# ── Stage 1: Restore (cached unless .csproj files change) ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

# Copy solution and all project files first for layer caching
COPY Argus.sln .
COPY src/Argus.Domain/Argus.Domain.csproj src/Argus.Domain/
COPY src/Argus.Infrastructure/Argus.Infrastructure.csproj src/Argus.Infrastructure/
COPY src/Argus.MarketDataSimulator/Argus.MarketDataSimulator.csproj src/Argus.MarketDataSimulator/
COPY src/Argus.TradeSimulator/Argus.TradeSimulator.csproj src/Argus.TradeSimulator/
COPY src/Argus.RiskEngine/Argus.RiskEngine.csproj src/Argus.RiskEngine/
COPY src/Argus.Api/Argus.Api.csproj src/Argus.Api/

# Restore just the target service (pulls transitive deps too)
ARG SERVICE_NAME
RUN dotnet restore "src/${SERVICE_NAME}/${SERVICE_NAME}.csproj"

# ── Stage 2: Build ──
FROM restore AS build
COPY src/ src/
ARG SERVICE_NAME
RUN dotnet publish "src/${SERVICE_NAME}/${SERVICE_NAME}.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 3: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# ASP.NET Core listens on 8080 by default in .NET 8 containers
EXPOSE 8080

COPY --from=build /app/publish .

ARG SERVICE_NAME
ENV SERVICE_DLL="${SERVICE_NAME}.dll"
ENTRYPOINT dotnet $SERVICE_DLL
