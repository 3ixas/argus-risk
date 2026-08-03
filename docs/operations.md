# Operations reference

This page contains the detailed addresses and commands used to inspect the local Argus environment. The defaults match `docker/docker-compose.yml`.

## Service addresses

| Service | Address | Purpose |
|---|---|---|
| Dashboard | `http://localhost:3000` | Next.js interface |
| API | `http://localhost:5050` | REST API and SignalR hub |
| Swagger UI | `http://localhost:5050/swagger` | Interactive API reference |
| Market data simulator | `http://localhost:5001/health` | Health and metrics |
| Trade simulator | `http://localhost:5002/health` | Health and metrics |
| Risk engine | `http://localhost:5003/health` | Health and metrics |
| Redpanda Console | `http://localhost:8080` | Kafka topic browser |
| Grafana | `http://localhost:3001` | Provisioned dashboards |
| Prometheus | `http://localhost:9090` | Metrics and queries |
| Jaeger | `http://localhost:16686` | Distributed traces |
| PostgreSQL | `localhost:5432` | Marten event store |

## REST API

Base URL: `http://localhost:5050`

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/positions` | List open positions |
| `GET` | `/api/positions/{instrumentId}` | Read one position |
| `GET` | `/api/risk/snapshot` | Read the latest risk snapshot |
| `GET` | `/api/instruments` | List reference instruments |
| `GET` | `/api/snapshots?from=&to=` | Read historical snapshots in a time range |
| `GET` | `/api/snapshots/count` | Count persisted snapshots |
| `GET` | `/api/alerts` | List unresolved alerts |
| `GET` | `/api/alerts/count` | Count unresolved alerts |
| `POST` | `/api/reconciliation/run` | Run event-replay reconciliation |
| `GET` | `/api/reconciliation/latest` | Read the latest reconciliation report |
| `POST` | `/api/replay/start` | Start historical replay |
| `POST` | `/api/replay/stop` | Stop replay |
| `POST` | `/api/replay/pause` | Pause replay |
| `POST` | `/api/replay/resume` | Resume replay |
| `GET` | `/api/replay/status` | Read replay state |
| `GET` | `/api/replay/available-range` | Read the persisted time range |

The SignalR hub is available at `/hubs/risk` and publishes `RiskUpdated`, `ReplayUpdate`, and `AlertReceived` events.

## Useful commands

Run the full environment:

```bash
docker compose -f docker/docker-compose.yml up --build
```

Inspect topics and messages:

```bash
docker exec argus-redpanda rpk topic list
docker exec argus-redpanda rpk topic consume market-data.prices --num 10
docker exec argus-redpanda rpk group describe argus-risk-engine
```

Reset the local environment, including persisted volumes:

```bash
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up
```

## Simulator configuration

The following `appsettings.json` keys can be overridden through normal .NET configuration providers, including environment variables:

| Key | Default | Purpose |
|---|---:|---|
| `Simulator:TickIntervalMs` | `100` | Price generation interval in milliseconds |
| `Simulator:Seed` | `42` | Deterministic random seed |
| `Simulator:BaseVolatility` | `0.20` | Annualised base volatility |
| `Simulator:SectorCorrelation` | `0.6` | Within-sector correlation |
| `Simulator:StressedMode` | `false` | Enables the high-volatility scenario |
