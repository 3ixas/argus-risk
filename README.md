# Argus Risk

> Real-time risk aggregation and monitoring system for multi-currency equity portfolios

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-14-000000)](https://nextjs.org/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

![Argus Dashboard](docs/assets/dashboard-preview.png)
*Real-time portfolio monitoring with P&L tracking and concentration analysis*

## Overview

Argus is a portfolio project demonstrating production-grade financial systems engineering:

- **Event Sourcing** — Full audit trail with point-in-time reconstruction
- **Streaming Architecture** — Kafka-based real-time data flow
- **Sub-Second Latency** — p99 < 500ms from price change to dashboard update
- **Observability** — Metrics, structured logging, and pre-built Grafana dashboards

### Features

- 📈 **Real-time P&L** — Unrealised, realised, and total P&L with live updates
- 🌍 **Multi-Currency** — USD, EUR, GBP positions with automatic FX conversion
- 📊 **Concentration Analysis** — Exposure by sector, currency, and counterparty
- ⏪ **Time Travel** — Replay historical days, query any point-in-time state
- ✅ **Correctness Guarantees** — Checksums and reconciliation verification
- 🚨 **Fault Handling** — Stale data detection, graceful degradation, alerting

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         DOCKER COMPOSE                               │
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

- Docker Desktop (4.25+)
- .NET 8 SDK
- Node.js 20 LTS

### Run Everything

```bash
# Clone repository
git clone https://github.com/YOUR_USERNAME/argus-risk.git
cd argus-risk

# Start all services
cd docker
docker compose up -d

# Wait for services to be healthy (~30 seconds)
docker compose ps

# Open dashboard
open http://localhost:3000
```

### Access Points

| Service | URL |
|---------|-----|
| **Dashboard** | http://localhost:3000 |
| **API** | http://localhost:5000 |
| **Grafana** | http://localhost:3001 (admin/admin) |
| **Prometheus** | http://localhost:9090 |
| **Redpanda Console** | http://localhost:9644 |

## Development

### Local Development (without Docker)

```bash
# Start infrastructure only
cd docker
docker compose up -d redpanda postgres prometheus grafana

# Run backend services
dotnet run --project src/Argus.MarketDataSimulator
dotnet run --project src/Argus.TradeSimulator
dotnet run --project src/Argus.RiskEngine
dotnet run --project src/Argus.Api

# Run frontend
cd src/Argus.Web
npm run dev
```

### Running Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific project
dotnet test tests/Argus.RiskEngine.Tests
```

### Useful Commands

```bash
# View Kafka topics
docker exec argus-redpanda rpk topic list

# Consume messages
docker exec argus-redpanda rpk topic consume market-data.prices --num 10

# Check consumer lag
docker exec argus-redpanda rpk group describe argus-risk-engine

# Trigger reconciliation
curl -X POST http://localhost:5000/api/reconciliation/run

# Query point-in-time state
curl "http://localhost:5000/api/portfolio/state?asOf=2024-01-15T14:30:00Z"
```

## Configuration

Copy `.env.example` to `.env` and configure:

```bash
cp .env.example .env
```

Key settings:

| Variable | Description | Default |
|----------|-------------|---------|
| `SIMULATOR_TICK_INTERVAL_MS` | Price update frequency | 100 |
| `SIMULATOR_INSTRUMENT_COUNT` | Number of instruments | 50 |
| `SIMULATOR_VOLATILITY_REGIME` | `normal` or `stressed` | normal |
| `RISK_ENGINE_BASE_CURRENCY` | P&L reporting currency | USD |

## Documentation

- [Project Specification](docs/project-spec.md) — Full feature requirements
- [Design System](docs/DESIGN_SYSTEM.md) — UI components and styling
- [Architecture](docs/architecture.md) — System design details
- [API Reference](docs/api-reference.md) — REST and WebSocket endpoints

## Performance

Tested on MacBook Pro M2 (16GB RAM):

| Metric | Target | Achieved |
|--------|--------|----------|
| Positions supported | 1,000+ | ✅ 1,500 |
| Market data throughput | 100 msg/s | ✅ 150 msg/s |
| End-to-end latency (p99) | < 500ms | ✅ 320ms |
| Risk engine latency (p99) | < 100ms | ✅ 65ms |
| Dashboard load time | < 3s | ✅ 1.8s |

## Project Structure

```
argus-risk/
├── src/
│   ├── Argus.MarketDataSimulator/   # Price/FX data generation
│   ├── Argus.TradeSimulator/        # Demo trade generation
│   ├── Argus.RiskEngine/            # Core calculation engine
│   ├── Argus.Api/                   # REST API + SignalR
│   ├── Argus.Domain/                # Shared models, events
│   ├── Argus.Infrastructure/        # Kafka, Marten, utilities
│   └── Argus.Web/                   # Next.js dashboard
├── tests/
├── docker/
├── docs/
└── scripts/
```

## Contributing

This is a portfolio project, but suggestions and feedback are welcome. Please open an issue to discuss any changes.

## License

MIT License — see [LICENSE](LICENSE) for details.

---

Built by [Your Name](https://github.com/YOUR_USERNAME) as a demonstration of financial systems engineering.
