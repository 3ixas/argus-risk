"use client";

import { useEffect, useMemo, useState } from "react";
import {
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  XAxis,
  YAxis,
  Tooltip,
} from "recharts";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useRisk } from "@/providers/risk-provider";
import { apiClient } from "@/lib/api-client";
import {
  sectorExposure,
  currencyExposure,
  topPositionsByPnl,
} from "@/lib/chart-data";
import { formatCurrency } from "@/lib/formatters";
import type { Instrument } from "@/types/domain";

// Colour palette for pie/bar charts
const COLORS = [
  "#3b82f6", "#8b5cf6", "#06b6d4", "#f59e0b", "#ef4444",
  "#10b981", "#ec4899", "#6366f1", "#14b8a6", "#f97316",
  "#84cc16",
];

export function ConcentrationCharts() {
  const { snapshot } = useRisk();
  const [instruments, setInstruments] = useState<Instrument[]>([]);

  useEffect(() => {
    apiClient.getInstruments().then(setInstruments).catch(() => {});
  }, []);

  const positions = snapshot?.positions ?? [];

  const sectorData = useMemo(
    () => sectorExposure(positions, instruments),
    [positions, instruments]
  );

  const currencyData = useMemo(
    () => currencyExposure(positions),
    [positions]
  );

  const topPnl = useMemo(
    () => topPositionsByPnl(positions, 10),
    [positions]
  );

  if (!snapshot) {
    return (
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Card key={i}>
            <CardHeader>
              <Skeleton className="h-5 w-32" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-48 w-full" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
      {/* Sector Exposure */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">
            Sector Exposure
          </CardTitle>
        </CardHeader>
        <CardContent className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={sectorData}
              layout="vertical"
              margin={{ top: 0, right: 0, bottom: 0, left: 0 }}
            >
              <XAxis type="number" hide />
              <YAxis
                type="category"
                dataKey="name"
                width={110}
                tick={{ fontSize: 11, fill: "#a1a1aa" }}
                axisLine={false}
                tickLine={false}
              />
              <Tooltip
                formatter={(value) => formatCurrency(Number(value), "USD", true)}
                contentStyle={{
                  background: "#18181b",
                  border: "1px solid #27272a",
                  borderRadius: 6,
                  fontSize: 12,
                }}
              />
              <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                {sectorData.map((_, i) => (
                  <Cell key={i} fill={COLORS[i % COLORS.length]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Currency Exposure */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">
            Currency Exposure
          </CardTitle>
        </CardHeader>
        <CardContent className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={currencyData}
                dataKey="value"
                nameKey="name"
                cx="50%"
                cy="50%"
                innerRadius="45%"
                outerRadius="75%"
                paddingAngle={2}
                label={({ name, percent }) =>
                  `${name ?? ""} ${((percent ?? 0) * 100).toFixed(0)}%`
                }
                labelLine={false}
              >
                {currencyData.map((_, i) => (
                  <Cell key={i} fill={COLORS[i % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip
                formatter={(value) => formatCurrency(Number(value), "USD", true)}
                contentStyle={{
                  background: "#18181b",
                  border: "1px solid #27272a",
                  borderRadius: 6,
                  fontSize: 12,
                }}
              />
            </PieChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Top 10 P&L */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">
            Top 10 P&L
          </CardTitle>
        </CardHeader>
        <CardContent className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={topPnl}
              layout="vertical"
              margin={{ top: 0, right: 0, bottom: 0, left: 0 }}
            >
              <XAxis type="number" hide />
              <YAxis
                type="category"
                dataKey="symbol"
                width={60}
                tick={{ fontSize: 11, fill: "#a1a1aa" }}
                axisLine={false}
                tickLine={false}
              />
              <Tooltip
                formatter={(value) => formatCurrency(Number(value), "USD")}
                contentStyle={{
                  background: "#18181b",
                  border: "1px solid #27272a",
                  borderRadius: 6,
                  fontSize: 12,
                }}
              />
              <Bar dataKey="pnl" radius={[0, 4, 4, 0]}>
                {topPnl.map((entry, i) => (
                  <Cell
                    key={i}
                    fill={entry.pnl >= 0 ? "#34d399" : "#f87171"}
                  />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </div>
  );
}
