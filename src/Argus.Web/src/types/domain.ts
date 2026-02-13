// Domain types mirroring C# models in Argus.Domain

export type Currency = "USD" | "EUR" | "GBP" | "JPY" | "CHF";

export type Sector =
  | "Technology"
  | "Healthcare"
  | "Finance"
  | "Energy"
  | "ConsumerDiscretionary"
  | "ConsumerStaples"
  | "Industrials"
  | "Materials"
  | "Utilities"
  | "RealEstate"
  | "Communications";

export type TradeSide = "Buy" | "Sell";

export interface Instrument {
  id: string;
  symbol: string;
  name: string;
  sector: Sector;
  currency: Currency;
  basePrice: number;
}

export interface Position {
  id: string;
  instrumentId: string;
  symbol: string;
  currency: Currency;
  side: TradeSide;
  quantity: number;
  averageCostBasis: number;
  realizedPnl: number;
  isOpen: boolean;
}

export interface PositionRisk {
  instrumentId: string;
  symbol: string;
  currency: Currency;
  side: TradeSide;
  quantity: number;
  averageCostBasis: number;
  currentPrice: number;
  unrealizedPnl: number;
  unrealizedPnlUsd: number;
  realizedPnl: number;
  realizedPnlUsd: number;
}

export interface RiskSnapshot {
  timestamp: string;
  positions: PositionRisk[];
  totalUnrealizedPnlUsd: number;
  totalRealizedPnlUsd: number;
  totalNetPnlUsd: number;
  positionCount: number;
  openPositionCount: number;
}

export interface PositionDiscrepancy {
  instrumentId: string;
  symbol: string;
  field: string;
  expected: string;
  actual: string;
  difference: number | null;
}

export interface ReconciliationReport {
  timestamp: string;
  totalEventsReplayed: number;
  expectedChecksum: string;
  actualChecksum: string;
  passed: boolean;
  discrepancies: PositionDiscrepancy[];
}
