"use client";

import { useRisk } from "@/providers/risk-provider";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, pnlColor } from "@/lib/formatters";
import { PnlSparkline } from "./pnl-sparkline";

function MetricCard({
  title,
  value,
  colorClass,
}: {
  title: string;
  value: string;
  colorClass?: string;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className={`text-2xl font-bold tabular-nums ${colorClass ?? ""}`}>
          {value}
        </p>
      </CardContent>
    </Card>
  );
}

function SkeletonCard() {
  return (
    <Card>
      <CardHeader className="pb-2">
        <Skeleton className="h-4 w-24" />
      </CardHeader>
      <CardContent>
        <Skeleton className="h-8 w-32" />
      </CardContent>
    </Card>
  );
}

export function PortfolioOverview() {
  const { snapshot } = useRisk();

  if (!snapshot) {
    return (
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-7">
        {Array.from({ length: 7 }).map((_, i) => (
          <SkeletonCard key={i} />
        ))}
      </div>
    );
  }

  const netPnl = snapshot.totalNetPnlUsd;
  const unrealized = snapshot.totalUnrealizedPnlUsd;
  const realized = snapshot.totalRealizedPnlUsd;

  const var95Value =
    snapshot.portfolioParametricVaR95 != null
      ? `P: ${formatCurrency(snapshot.portfolioParametricVaR95)} · H: ${formatCurrency(snapshot.portfolioHistoricalVaR95!)}`
      : "—";

  const var99Value =
    snapshot.portfolioParametricVaR99 != null
      ? `P: ${formatCurrency(snapshot.portfolioParametricVaR99)} · H: ${formatCurrency(snapshot.portfolioHistoricalVaR99!)}`
      : "—";

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-7">
      <MetricCard
        title="Net P&L"
        value={formatCurrency(netPnl, "USD", true)}
        colorClass={pnlColor(netPnl)}
      />
      <MetricCard
        title="Unrealised P&L"
        value={formatCurrency(unrealized, "USD", true)}
        colorClass={pnlColor(unrealized)}
      />
      <MetricCard
        title="Realised P&L"
        value={formatCurrency(realized, "USD", true)}
        colorClass={pnlColor(realized)}
      />
      <MetricCard
        title="Open Positions"
        value={snapshot.openPositionCount.toString()}
      />
      <MetricCard
        title="VaR 95% (1-day)"
        value={var95Value}
      />
      <MetricCard
        title="VaR 99% (1-day)"
        value={var99Value}
      />
      <Card className="col-span-2 lg:col-span-1">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">
            P&L Trend
          </CardTitle>
        </CardHeader>
        <CardContent className="h-16">
          <PnlSparkline />
        </CardContent>
      </Card>
    </div>
  );
}
