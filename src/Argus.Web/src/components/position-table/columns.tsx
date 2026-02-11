"use client";

import { ColumnDef } from "@tanstack/react-table";
import type { PositionRisk } from "@/types/domain";
import { formatCurrency, pnlColor } from "@/lib/formatters";

export const columns: ColumnDef<PositionRisk>[] = [
  {
    accessorKey: "symbol",
    header: "Symbol",
    cell: ({ getValue }) => (
      <span className="font-medium">{getValue<string>()}</span>
    ),
    size: 100,
  },
  {
    accessorKey: "side",
    header: "Side",
    cell: ({ getValue }) => {
      const side = getValue<string>();
      return (
        <span
          className={side === "Buy" ? "text-emerald-400" : "text-red-400"}
        >
          {side === "Buy" ? "Long" : "Short"}
        </span>
      );
    },
    size: 80,
  },
  {
    accessorKey: "quantity",
    header: "Qty",
    cell: ({ getValue }) => (
      <span className="tabular-nums">{getValue<number>().toLocaleString()}</span>
    ),
    size: 80,
  },
  {
    accessorKey: "currentPrice",
    header: "Price",
    cell: ({ row }) => (
      <span className="tabular-nums">
        {formatCurrency(row.original.currentPrice, row.original.currency)}
      </span>
    ),
    size: 110,
  },
  {
    accessorKey: "currency",
    header: "Ccy",
    size: 60,
  },
  {
    id: "marketValue",
    header: "Mkt Value",
    accessorFn: (row) => row.currentPrice * row.quantity,
    cell: ({ getValue, row }) => (
      <span className="tabular-nums">
        {formatCurrency(getValue<number>(), row.original.currency)}
      </span>
    ),
    size: 120,
  },
  {
    accessorKey: "unrealizedPnlUsd",
    header: "Unreal P&L",
    cell: ({ getValue }) => {
      const val = getValue<number>();
      return (
        <span className={`tabular-nums ${pnlColor(val)}`}>
          {formatCurrency(val, "USD")}
        </span>
      );
    },
    size: 120,
  },
  {
    id: "pnlPercent",
    header: "P&L %",
    accessorFn: (row) => {
      const costBasis = row.averageCostBasis * row.quantity;
      if (costBasis === 0) return 0;
      return row.unrealizedPnl / Math.abs(costBasis);
    },
    cell: ({ getValue }) => {
      const val = getValue<number>();
      const sign = val > 0 ? "+" : "";
      return (
        <span className={`tabular-nums ${pnlColor(val)}`}>
          {sign}{(val * 100).toFixed(2)}%
        </span>
      );
    },
    size: 90,
  },
  {
    id: "weight",
    header: "Weight",
    // Weight is calculated as |market value| / total |market value| — set per-render via meta
    cell: ({ row, table }) => {
      const mv = Math.abs(row.original.currentPrice * row.original.quantity);
      const totalMv = (table.options.meta as { totalMarketValue?: number })
        ?.totalMarketValue ?? 1;
      const weight = totalMv > 0 ? mv / totalMv : 0;
      return (
        <span className="tabular-nums text-muted-foreground">
          {(weight * 100).toFixed(1)}%
        </span>
      );
    },
    size: 80,
  },
];
