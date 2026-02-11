import { describe, it, expect } from "vitest";
import {
  formatCurrency,
  formatPercent,
  pnlColor,
  freshnessColor,
} from "@/lib/formatters";

describe("formatCurrency", () => {
  it("formats positive USD values", () => {
    expect(formatCurrency(1234.56)).toBe("$1,234.56");
  });

  it("formats negative values with sign", () => {
    expect(formatCurrency(-500)).toBe("-$500.00");
  });

  it("formats zero", () => {
    expect(formatCurrency(0)).toBe("$0.00");
  });

  it("uses currency symbol for EUR", () => {
    expect(formatCurrency(100, "EUR")).toBe("€100.00");
  });

  it("uses currency symbol for GBP", () => {
    expect(formatCurrency(100, "GBP")).toBe("£100.00");
  });

  it("uses currency symbol for JPY", () => {
    expect(formatCurrency(100, "JPY")).toBe("¥100.00");
  });

  it("compact mode: thousands", () => {
    expect(formatCurrency(5432, "USD", true)).toBe("$5.4K");
  });

  it("compact mode: millions", () => {
    expect(formatCurrency(2_345_000, "USD", true)).toBe("$2.3M");
  });

  it("compact mode: billions", () => {
    expect(formatCurrency(1_500_000_000, "USD", true)).toBe("$1.5B");
  });

  it("compact mode: small values stay normal", () => {
    expect(formatCurrency(42.5, "USD", true)).toBe("$42.50");
  });

  it("compact mode: negative millions", () => {
    expect(formatCurrency(-3_200_000, "USD", true)).toBe("-$3.2M");
  });
});

describe("formatPercent", () => {
  it("formats positive percentages with + sign", () => {
    expect(formatPercent(0.0532)).toBe("+5.32%");
  });

  it("formats negative percentages", () => {
    expect(formatPercent(-0.1)).toBe("-10.00%");
  });

  it("formats zero", () => {
    expect(formatPercent(0)).toBe("0.00%");
  });
});

describe("pnlColor", () => {
  it("returns green for positive", () => {
    expect(pnlColor(100)).toBe("text-emerald-400");
  });

  it("returns red for negative", () => {
    expect(pnlColor(-50)).toBe("text-red-400");
  });

  it("returns muted for zero", () => {
    expect(pnlColor(0)).toBe("text-muted-foreground");
  });
});

describe("freshnessColor", () => {
  it("returns green for fresh data (<5s)", () => {
    expect(freshnessColor(2)).toBe("text-emerald-400");
  });

  it("returns yellow for stale data (5-15s)", () => {
    expect(freshnessColor(10)).toBe("text-yellow-400");
  });

  it("returns red for very stale data (>15s)", () => {
    expect(freshnessColor(30)).toBe("text-red-400");
  });

  it("boundary: exactly 5s is yellow", () => {
    expect(freshnessColor(5)).toBe("text-yellow-400");
  });

  it("boundary: exactly 15s is red", () => {
    expect(freshnessColor(15)).toBe("text-red-400");
  });
});
