import type { Instrument, PositionRisk, Sector, Currency } from "@/types/domain";

export interface ExposureEntry {
  name: string;
  value: number;
}

/**
 * Aggregate positions by sector using instrument reference data for the lookup.
 * Returns absolute market value per sector, sorted descending.
 */
export function sectorExposure(
  positions: PositionRisk[],
  instruments: Instrument[]
): ExposureEntry[] {
  const instrumentMap = new Map(instruments.map((i) => [i.id, i]));
  const sectorTotals = new Map<Sector, number>();

  for (const pos of positions) {
    const instrument = instrumentMap.get(pos.instrumentId);
    if (!instrument) continue;
    const sector = instrument.sector;
    const mv = Math.abs(pos.currentPrice * pos.quantity);
    sectorTotals.set(sector, (sectorTotals.get(sector) ?? 0) + mv);
  }

  return Array.from(sectorTotals.entries())
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value);
}

/**
 * Aggregate positions by currency. Returns absolute market value per currency.
 */
export function currencyExposure(positions: PositionRisk[]): ExposureEntry[] {
  const totals = new Map<Currency, number>();

  for (const pos of positions) {
    const mv = Math.abs(pos.currentPrice * pos.quantity);
    totals.set(pos.currency, (totals.get(pos.currency) ?? 0) + mv);
  }

  return Array.from(totals.entries())
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value);
}

/**
 * Get top N positions by absolute unrealised P&L (USD), preserving sign.
 */
export function topPositionsByPnl(
  positions: PositionRisk[],
  n = 10
): { symbol: string; pnl: number }[] {
  return [...positions]
    .sort((a, b) => Math.abs(b.unrealizedPnlUsd) - Math.abs(a.unrealizedPnlUsd))
    .slice(0, n)
    .map((p) => ({ symbol: p.symbol, pnl: p.unrealizedPnlUsd }));
}
