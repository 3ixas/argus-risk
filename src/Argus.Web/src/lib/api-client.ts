import type { Instrument, Position, RiskSnapshot } from "@/types/domain";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

async function fetchJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_URL}${path}`);
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
};
