"use client";

import { useRisk } from "@/providers/risk-provider";
import {
  AreaChart,
  Area,
  ResponsiveContainer,
  YAxis,
  ReferenceLine,
} from "recharts";

export function PnlSparkline() {
  const { pnlHistory } = useRisk();

  if (pnlHistory.length < 2) {
    return (
      <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
        Waiting for data...
      </div>
    );
  }

  const latestValue = pnlHistory[pnlHistory.length - 1].value;
  const isPositive = latestValue >= 0;

  return (
    <ResponsiveContainer width="100%" height="100%">
      <AreaChart data={pnlHistory} margin={{ top: 4, right: 4, bottom: 4, left: 4 }}>
        <defs>
          <linearGradient id="pnlGradient" x1="0" y1="0" x2="0" y2="1">
            <stop
              offset="5%"
              stopColor={isPositive ? "#34d399" : "#f87171"}
              stopOpacity={0.3}
            />
            <stop
              offset="95%"
              stopColor={isPositive ? "#34d399" : "#f87171"}
              stopOpacity={0}
            />
          </linearGradient>
        </defs>
        <YAxis domain={["dataMin", "dataMax"]} hide />
        <ReferenceLine y={0} stroke="#525252" strokeDasharray="3 3" />
        <Area
          type="monotone"
          dataKey="value"
          stroke={isPositive ? "#34d399" : "#f87171"}
          strokeWidth={1.5}
          fill="url(#pnlGradient)"
          isAnimationActive={false}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
