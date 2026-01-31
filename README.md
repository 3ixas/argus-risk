# Argus Risk

> Real-time risk aggregation and monitoring system for multi-currency equity portfolios

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-14-000000)](https://nextjs.org/)

## Overview

Argus demonstrates production-grade financial systems engineering:

- **Event Sourcing** — Full audit trail with point-in-time reconstruction
- **Streaming Architecture** — Kafka-based real-time data flow
- **Sub-Second Latency** — Target p99 < 500ms from price change to dashboard
- **Observability** — Metrics, structured logging, and Grafana dashboards

### Features

- **Real-time P&L** — Unrealised, realised, and total P&L with live updates
- **Multi-Currency** — USD, EUR, GBP, JPY, CHF positions with automatic FX conversion
- **Concentration Analysis** — Exposure by sector, currency, and instrument
- **Time Travel** — Replay historical state, query any point-in-time
- **Correctness Guarantees** — Checksums and reconciliation verification

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         DOCKER COMPOSE                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Market Data          Redpanda           Risk Engine                │
│  Simulator     ────▶  (Kafka)    ────▶   (Aggregation)              │
│                                                 │                   │
│  Trade                                          ▼                   │
│  Simulator     ────▶             ────▶   API Gateway                │
│                                          (REST + SignalR)           │
│                                                 │                   │
│  PostgreSQL ◀────────────────────────────────────                   │
│  (Marten Event Store)                           │ WebSocket         │
│                                                 ▼                   │
│  Prometheus ◀──── All Services          Next.js Dashboard           │
│       │                                                             │
│       ▼                                                             │
│  Grafana                                                            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Core Engine** | C# / .NET 8 |
| **Event Store** | Marten (PostgreSQL) |
| **Messaging** | Redpanda (Kafka-compatible) |
| **Real-time API** | SignalR |
| **Observability** | OpenTelemetry, Prometheus, Grafana |
| **Frontend** | Next.js 14, TypeScript, Tailwind CSS |
| **UI Components** | shadcn/ui, Recharts, TanStack Table |

## Quick Start

### Prerequisites

- [Docker Desktop](https://docker.com/products/docker-desktop) (4.25+)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 20 LTS (for frontend, coming soon)

### 1. Start Infrastructure

```bash
# Clone repository
git clone https://github.com/3ixas/argus-risk.git
cd argus-risk

# Start Redpanda and PostgreSQL
cd docker
docker compose up -d

# Create Kafka topics
cd ..
./scripts/create-topics.sh
```

### 2. Run the Market Data Simulator

```bash
dotnet run --project src/Argus.MarketDataSimulator
```

### 3. Verify It's Working

```bash
# Check health endpoint
curl http://localhost:5001/health

# View messages in Kafka (in another terminal)
docker exec argus-redpanda rpk topic consume market-data.prices --num 5
```

### Access Points

| Service | URL |
|---------|-----|
| **Market Data Simulator** | http://localhost:5001 |
| **Redpanda Console** | http://localhost:8080 |
| **PostgreSQL** | localhost:5432 (argus/argus) |

## Configuration

Copy `.env.example` to `.env` and configure:

```bash
cp .env.example .env
```

Key settings in `appsettings.json`:

| Variable | Description | Default |
|----------|-------------|---------|
| `Simulator:TickIntervalMs` | Price update frequency (ms) | 100 |
| `Simulator:Seed` | RNG seed for deterministic replay | 42 |
| `Simulator:BaseVolatility` | Annualised volatility (0.20 = 20%) | 0.20 |
| `Simulator:SectorCorrelation` | Correlation within sectors (0-1) | 0.6 |
| `Simulator:StressedMode` | Enable high volatility mode | false |

## Development

### Useful Commands

```bash
# Build solution
dotnet build

# Run tests
dotnet test

# View Kafka topics
docker exec argus-redpanda rpk topic list

# Consume price messages
docker exec argus-redpanda rpk topic consume market-data.prices --num 10

# Consume FX messages
docker exec argus-redpanda rpk topic consume market-data.fx --num 5

# Reset infrastructure
cd docker && docker compose down -v && docker compose up -d
```

## Project Structure

```
argus-risk/
├── src/
│   ├── Argus.Domain/                # Shared models, events, enums
│   ├── Argus.Infrastructure/        # Kafka producer, utilities
│   └── Argus.MarketDataSimulator/   # Price/FX data generation
├── docker/
│   └── docker-compose.yml           # Redpanda + PostgreSQL
├── scripts/
│   └── create-topics.sh             # Kafka topic setup
└── docs/
    └── project-spec.md              # Full requirements
```

## Roadmap

- [x] **Phase 1**: Foundation — Solution structure, Docker infrastructure
- [x] **Feature 1**: Market Data Simulator — GBM price generation, sector correlation
- [ ] **Feature 2**: Trade Ingestion — Kafka consumer, Marten event sourcing
- [ ] **Feature 3**: Risk Engine — P&L calculation, multi-currency aggregation
- [ ] **Feature 4**: REST API — Endpoints + SignalR real-time hub
- [ ] **Feature 5**: Dashboard — Next.js with live updates

## Performance Targets

| Metric | Target |
|--------|--------|
| Positions supported | 1,000+ |
| Market data throughput | 100+ msg/s |
| End-to-end latency (p99) | < 500ms |
| Risk engine latency (p99) | < 100ms |

---

Built as a demonstration of financial systems engineering.
