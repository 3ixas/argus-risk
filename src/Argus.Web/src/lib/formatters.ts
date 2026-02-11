import type { Currency } from "@/types/domain";

const currencySymbols: Record<Currency, string> = {
  USD: "$",
  EUR: "€",
  GBP: "£",
  JPY: "¥",
  CHF: "CHF ",
};

/**
 * Format a number as currency with appropriate symbol and decimal places.
 * Compact mode uses K/M/B suffixes for large values.
 */
export function formatCurrency(
  value: number,
  currency: Currency = "USD",
  compact = false
): string {
  const symbol = currencySymbols[currency];
  const sign = value < 0 ? "-" : "";
  const abs = Math.abs(value);

  if (compact && abs >= 1_000_000_000) {
    return `${sign}${symbol}${(abs / 1_000_000_000).toFixed(1)}B`;
  }
  if (compact && abs >= 1_000_000) {
    return `${sign}${symbol}${(abs / 1_000_000).toFixed(1)}M`;
  }
  if (compact && abs >= 1_000) {
    return `${sign}${symbol}${(abs / 1_000).toFixed(1)}K`;
  }

  return `${sign}${symbol}${abs.toLocaleString("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/** Format a decimal as a percentage string (e.g. 0.0532 → "+5.32%") */
export function formatPercent(value: number): string {
  const sign = value > 0 ? "+" : "";
  return `${sign}${(value * 100).toFixed(2)}%`;
}

/** Returns a Tailwind text color class for P&L values: green for positive, red for negative, muted for zero */
export function pnlColor(value: number): string {
  if (value > 0) return "text-emerald-400";
  if (value < 0) return "text-red-400";
  return "text-muted-foreground";
}

/**
 * Returns a color class indicating data freshness based on age in seconds.
 * Green (<5s), yellow (5–15s), red (>15s).
 */
export function freshnessColor(ageSeconds: number): string {
  if (ageSeconds < 5) return "text-emerald-400";
  if (ageSeconds < 15) return "text-yellow-400";
  return "text-red-400";
}
