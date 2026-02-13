import type {
  Instrument,
  Position,
  ReconciliationReport,
  RiskSnapshot,
} from "@/types/domain";

// Browser uses NEXT_PUBLIC_API_URL (baked at build time, e.g. http://localhost:5000)
// Server-side rendering uses API_URL (runtime env var, e.g. http://argus-api:8080 in Docker)
const API_URL =
  (typeof window === "undefined" ? process.env.API_URL : undefined) ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5000";

async function fetchJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_URL}${path}`);
  if (!res.ok) {
    throw new Error(`API ${res.status}: ${path}`);
  }
  return res.json();
}

async function postJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, { method: "POST" });
  if (!res.ok) {
    throw new Error(`API ${res.status}: ${path}`);
  }
  return res.json();
}

export const apiClient = {
  getInstruments: () => fetchJson<Instrument[]>("/api/instruments"),
  getPositions: () => fetchJson<Position[]>("/api/positions"),
  getPosition: (instrumentId: string) =>
    fetchJson<Position>(`/api/positions/${instrumentId}`),
  getRiskSnapshot: () => fetchJson<RiskSnapshot>("/api/risk/snapshot"),
  runReconciliation: () =>
    postJson<ReconciliationReport>("/api/reconciliation/run"),
  getLatestReconciliation: async () => {
    const res = await fetch(`${API_URL}/api/reconciliation/latest`);
    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`API ${res.status}: /api/reconciliation/latest`);
    return res.json() as Promise<ReconciliationReport>;
  },
};
