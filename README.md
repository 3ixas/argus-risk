# Argus Risk

> Real-time risk aggregation and monitoring system for multi-currency equity portfolios

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-14-000000)](https://nextjs.org/)
[![CI](https://github.com/3ixas/argus-risk/actions/workflows/ci.yml/badge.svg)](https://github.com/3ixas/argus-risk/actions/workflows/ci.yml)

## Overview

Argus demonstrates production-grade financial systems engineering:

- **Event Sourcing** — Full audit trail with point-in-time reconstruction via Marten
- **Streaming Architecture** — Kafka-based real-time data flow across all services
- **Sub-Second Latency** — Target p99 < 500ms from price change to dashboard
- **Observability** — OpenTelemetry traces, Prometheus metrics, and Grafana dashboards
- **Fault Tolerance** — Circuit breakers, staleness detection, and alert deduplication
- **Replay Mode** — Scrub through historical portfolio state at 1x/5x/10x/60x speed

### Features

- **Real-time P&L** — Unrealised, realised, and total P&L with live SignalR updates
- **Multi-Currency** — USD, EUR, GBP, JPY, CHF positions with automatic FX conversion
- **Concentration Analysis** — Exposure breakdown by sector, currency, and instrument
- **Time Travel** — Replay historical state and query any point-in-time snapshot
- **Correctness Guarantees** — SHA-256 checksums and full reconciliation verification
- **Alerting** — Kafka-based alert pipeline with deduplication and resolution tracking
- **Observability** — Distributed tracing, 16 custom metrics, pre-built Grafana dashboards

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

**Data flow:** Market data and trades enter via Kafka topics → the Risk Engine aggregates positions using FIFO cost basis → snapshots are published back to Kafka and persisted to PostgreSQL → the API broadcasts updates over SignalR → the dashboard renders live.

→ See [Architecture Deep-Dive](docs/architecture.md) for detailed Mermaid diagrams

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Core Engine** | C# / .NET 8 |
| **Event Store** | Marten 7.x (PostgreSQL) |
| **Messaging** | Redpanda (Kafka-compatible) |
| **Real-time API** | ASP.NET Core Minimal APIs + SignalR |
| **Observability** | OpenTelemetry, Prometheus, Grafana, Jaeger |
| **Frontend** | Next.js 14, TypeScript, Tailwind CSS |
| **UI Components** | shadcn/ui, Recharts, TanStack Table |

## Quick Start

### Prerequisites

- [Docker Desktop](https://docker.com/products/docker-desktop) 4.25+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local development)
- [Node.js 20 LTS](https://nodejs.org/) (for local frontend development)

### Single-Command Start

```bash
git clone https://github.com/3ixas/argus-risk.git
cd argus-risk/docker
docker compose up
```

Open **http://localhost:3000** — the full stack starts automatically, including topic creation, simulators, risk engine, API, and dashboard.

> First run takes ~2 minutes as Docker pulls images. Subsequent starts are ~10 seconds.

### Access Points

| Service | URL | Description |
|---------|-----|-------------|
| **Dashboard** | http://localhost:3000 | Next.js frontend |
| **API** | http://localhost:5050 | REST + SignalR hub |
| **Swagger UI** | http://localhost:5050/swagger | Interactive API documentation |
| **Market Data Simulator** | http://localhost:5001/health | Health + metrics |
| **Trade Simulator** | http://localhost:5002/health | Health + metrics |
| **Risk Engine** | http://localhost:5003/health | Health + metrics |
| **Redpanda Console** | http://localhost:8080 | Kafka topic browser |
| **Grafana** | http://localhost:3001 | Pre-built dashboards |
| **Prometheus** | http://localhost:9090 | Metrics + query UI |
| **Jaeger** | http://localhost:16686 | Distributed traces |
| **PostgreSQL** | localhost:5432 | argus/argus |

## API Reference

Base URL: `http://localhost:5050`

### Positions

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/positions` | All open positions |
| `GET` | `/api/positions/{instrumentId}` | Single position by instrument GUID |

### Risk

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/risk/snapshot` | Latest aggregated risk snapshot |

### Instruments

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/instruments` | All instruments (reference data) |

### Snapshots

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/snapshots?from=&to=` | Historical snapshots in time range (max 1 hour) |
| `GET` | `/api/snapshots/count` | Total persisted snapshot count |

### Alerts

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/alerts` | Active (unresolved) alerts |
| `GET` | `/api/alerts/count` | Active alert count |

### Reconciliation

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/reconciliation/run` | Run full event replay reconciliation |
| `GET` | `/api/reconciliation/latest` | Latest reconciliation report |

### Replay

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/replay/start` | Start replay — body: `{ startTime, endTime, speed }` |
| `POST` | `/api/replay/stop` | Stop active replay session |
| `POST` | `/api/replay/pause` | Pause replay |
| `POST` | `/api/replay/resume` | Resume paused replay |
| `GET` | `/api/replay/status` | Current replay state |
| `GET` | `/api/replay/available-range` | Earliest and latest snapshot timestamps |

### SignalR Hub

Connect to `/hubs/risk` for real-time push events:

| Event | Description |
|-------|-------------|
| `RiskUpdated` | Live risk snapshot (1Hz) |
| `ReplayUpdate` | Historical snapshot during replay |
| `AlertReceived` | New or resolved alert |

## Project Structure

```
argus-risk/
├── src/
│   ├── Argus.Domain/                # Shared models, events, enums
│   ├── Argus.Infrastructure/        # Kafka producer/consumer, Marten setup
│   ├── Argus.MarketDataSimulator/   # GBM price + FX generation (100+ msg/s)
│   ├── Argus.TradeSimulator/        # Trade generation with Kafka consumer
│   ├── Argus.RiskEngine/            # FIFO P&L, multi-currency aggregation
│   ├── Argus.Api/                   # Minimal APIs + SignalR hub
│   └── Argus.Web/                   # Next.js 14 dashboard
├── tests/
│   ├── Argus.RiskEngine.Tests/      # Unit tests (63 tests)
│   └── Argus.Api.Tests/             # API unit tests (39 tests)
├── docker/
│   ├── docker-compose.yml           # Full stack (12 services)
│   ├── dotnet.Dockerfile            # Multi-stage build for all .NET services
│   ├── web.Dockerfile               # Next.js standalone build
│   ├── grafana/                     # Pre-provisioned dashboards
│   └── prometheus/                  # Scrape config
├── docs/
│   └── project-spec.md              # Full feature requirements
└── scripts/
    └── seed-data.sql                # Reference instrument data
```

## Development

### Running Tests

```bash
# All tests (134 total)
dotnet test

# With output
dotnet test --logger "console;verbosity=normal"
```

### Useful Commands

```bash
# Build solution
dotnet build

# View Kafka topics
docker exec argus-redpanda rpk topic list

# Consume price messages
docker exec argus-redpanda rpk topic consume market-data.prices --num 10

# Check consumer group lag
docker exec argus-redpanda rpk group describe argus-risk-engine

# Rebuild a single service
docker compose -f docker/docker-compose.yml up -d --build argus-risk-engine

# Reset everything (clears volumes)
docker compose -f docker/docker-compose.yml down -v && docker compose -f docker/docker-compose.yml up
```

### Frontend Development

```bash
cd src/Argus.Web
npm install
npm run dev   # http://localhost:3000
```

## Observability

Grafana at **http://localhost:3001** includes two pre-built dashboards:

- **Risk Metrics** — P&L throughput, position counts, snapshot latency, replay sessions, fault handling (active alerts, circuit breaker state, stale positions)
- **System Health** — Service health, Kafka lag, HTTP request rates across all services

Jaeger at **http://localhost:16686** shows distributed traces with W3C `traceparent` propagation across Kafka message boundaries.

## Configuration

Key settings in `appsettings.json` (overridable via environment variables):

| Variable | Description | Default |
|----------|-------------|---------|
| `Simulator:TickIntervalMs` | Price update frequency (ms) | 100 |
| `Simulator:Seed` | RNG seed for deterministic replay | 42 |
| `Simulator:BaseVolatility` | Annualised volatility (0.20 = 20%) | 0.20 |
| `Simulator:SectorCorrelation` | Correlation within sectors (0-1) | 0.6 |
| `Simulator:StressedMode` | Enable high volatility mode | false |

## Performance Targets

| Metric | Target |
|--------|--------|
| Positions supported | 1,000+ |
| Market data throughput | 100+ msg/s |
| End-to-end latency (p99) | < 500ms |
| Risk engine latency (p99) | < 100ms |

## Roadmap

### Version 1 (Complete)

- [x] **Phase 1**: Solution structure + Docker infrastructure
- [x] **Feature 1**: Market Data Simulator — GBM price generation, sector correlation
- [x] **Feature 2**: Trade Ingestion — Kafka consumer, FIFO cost basis, Marten event sourcing
- [x] **Feature 3**: Risk Engine — P&L calculation, multi-currency aggregation, 1Hz snapshots
- [x] **Feature 4**: REST API — 16 endpoints + SignalR real-time hub
- [x] **Feature 5**: Dashboard — Next.js with live updates, virtualised table, Recharts
- [x] **Feature 6**: Full Dockerization — single `docker compose up` experience
- [x] **Feature 7**: Correctness & Reconciliation — SHA-256 checksums, event replay verification
- [x] **Feature 8**: Observability — OpenTelemetry, 16 custom metrics, Grafana dashboards
- [x] **Feature 9**: Replay Mode — historical playback at 1x/5x/10x/60x
- [x] **Feature 10**: Fault Handling & Alerting — circuit breakers, staleness detection, alert pipeline

### Version 2 (Ideas)

- [ ] Options Greeks — Delta, Gamma, Vega risk on derivatives
- [ ] VaR / CVaR — Historical simulation and Monte Carlo Value-at-Risk
- [ ] Order Book Simulation — Limit order book with bid-ask dynamics
- [ ] Multi-Portfolio — Separate books with cross-portfolio netting
- [ ] Stress Testing — Scenario analysis with user-defined shocks

---

Built as a demonstration of financial systems engineering.
