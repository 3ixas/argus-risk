import { describe, it, expect } from "vitest";
import {
  sectorExposure,
  currencyExposure,
  topPositionsByPnl,
} from "@/lib/chart-data";
import type { Instrument, PositionRisk } from "@/types/domain";

// Helper to create a minimal PositionRisk
function makePosition(overrides: Partial<PositionRisk>): PositionRisk {
  return {
    instrumentId: "00000000-0000-0000-0000-000000000001",
    symbol: "AAPL",
    currency: "USD",
    side: "Buy",
    quantity: 100,
    averageCostBasis: 150,
    currentPrice: 155,
    unrealizedPnl: 500,
    unrealizedPnlUsd: 500,
    realizedPnl: 0,
    realizedPnlUsd: 0,
    ...overrides,
  };
}

function makeInstrument(overrides: Partial<Instrument>): Instrument {
  return {
    id: "00000000-0000-0000-0000-000000000001",
    symbol: "AAPL",
    name: "Apple Inc.",
    sector: "Technology",
    currency: "USD",
    basePrice: 150,
    ...overrides,
  };
}

describe("currencyExposure", () => {
  it("aggregates positions by currency", () => {
    const positions = [
      makePosition({ currency: "USD", currentPrice: 100, quantity: 10 }),
      makePosition({ currency: "USD", currentPrice: 50, quantity: 20 }),
      makePosition({ currency: "EUR", currentPrice: 200, quantity: 5 }),
    ];

    const result = currencyExposure(positions);

    expect(result).toHaveLength(2);
    // USD: |100*10| + |50*20| = 2000
    expect(result[0]).toEqual({ name: "USD", value: 2000 });
    // EUR: |200*5| = 1000
    expect(result[1]).toEqual({ name: "EUR", value: 1000 });
  });

  it("returns sorted by value descending", () => {
    const positions = [
      makePosition({ currency: "GBP", currentPrice: 10, quantity: 1 }),
      makePosition({ currency: "USD", currentPrice: 100, quantity: 100 }),
    ];

    const result = currencyExposure(positions);
    expect(result[0].name).toBe("USD");
    expect(result[1].name).toBe("GBP");
  });

  it("handles empty positions", () => {
    expect(currencyExposure([])).toEqual([]);
  });
});

describe("sectorExposure", () => {
  it("aggregates positions by sector via instrument lookup", () => {
    const id1 = "00000000-0000-0000-0000-000000000001";
    const id2 = "00000000-0000-0000-0000-000000000002";

    const positions = [
      makePosition({ instrumentId: id1, currentPrice: 100, quantity: 10 }),
      makePosition({ instrumentId: id2, currentPrice: 200, quantity: 5 }),
    ];

    const instruments = [
      makeInstrument({ id: id1, sector: "Technology" }),
      makeInstrument({ id: id2, sector: "Healthcare" }),
    ];

    const result = sectorExposure(positions, instruments);

    expect(result).toHaveLength(2);
    // Both have same market value (1000), so order depends on sort stability
    const tech = result.find((e) => e.name === "Technology");
    const health = result.find((e) => e.name === "Healthcare");
    expect(tech?.value).toBe(1000);
    expect(health?.value).toBe(1000);
  });

  it("skips positions with no matching instrument", () => {
    const positions = [
      makePosition({ instrumentId: "unknown-id", currentPrice: 100, quantity: 10 }),
    ];

    const result = sectorExposure(positions, []);
    expect(result).toEqual([]);
  });

  it("combines positions in the same sector", () => {
    const id1 = "00000000-0000-0000-0000-000000000001";
    const id2 = "00000000-0000-0000-0000-000000000002";

    const positions = [
      makePosition({ instrumentId: id1, currentPrice: 100, quantity: 10 }),
      makePosition({ instrumentId: id2, currentPrice: 50, quantity: 20 }),
    ];

    const instruments = [
      makeInstrument({ id: id1, sector: "Technology" }),
      makeInstrument({ id: id2, sector: "Technology" }),
    ];

    const result = sectorExposure(positions, instruments);
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({ name: "Technology", value: 2000 });
  });
});

describe("topPositionsByPnl", () => {
  it("returns top N by absolute unrealised P&L", () => {
    const positions = [
      makePosition({ symbol: "AAPL", unrealizedPnlUsd: 100 }),
      makePosition({ symbol: "MSFT", unrealizedPnlUsd: -300 }),
      makePosition({ symbol: "GOOG", unrealizedPnlUsd: 200 }),
    ];

    const result = topPositionsByPnl(positions, 2);
    expect(result).toHaveLength(2);
    // MSFT has highest absolute P&L (300), then GOOG (200)
    expect(result[0]).toEqual({ symbol: "MSFT", pnl: -300 });
    expect(result[1]).toEqual({ symbol: "GOOG", pnl: 200 });
  });

  it("preserves sign of P&L", () => {
    const positions = [
      makePosition({ symbol: "LOSS", unrealizedPnlUsd: -500 }),
    ];

    const result = topPositionsByPnl(positions, 10);
    expect(result[0].pnl).toBe(-500);
  });

  it("defaults to top 10", () => {
    const positions = Array.from({ length: 15 }, (_, i) =>
      makePosition({ symbol: `S${i}`, unrealizedPnlUsd: i * 100 })
    );

    const result = topPositionsByPnl(positions);
    expect(result).toHaveLength(10);
  });

  it("handles empty positions", () => {
    expect(topPositionsByPnl([])).toEqual([]);
  });
});
