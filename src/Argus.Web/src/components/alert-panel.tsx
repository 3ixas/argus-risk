"use client";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useRisk } from "@/providers/risk-provider";
import type { Alert, AlertSeverity } from "@/types/domain";

function formatRelativeTime(timestamp: string): string {
  const seconds = Math.round((Date.now() - new Date(timestamp).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  return `${Math.floor(minutes / 60)}h ago`;
}

function severityDot({ severity }: { severity: AlertSeverity }) {
  return (
    <span
      className={`inline-block h-2 w-2 shrink-0 rounded-full ${
        severity === "Error" ? "bg-red-400" : "bg-yellow-400"
      }`}
    />
  );
}

function AlertRow({ alert }: { alert: Alert }) {
  return (
    <div className="flex items-start gap-2 py-1.5 text-xs border-b border-border/40 last:border-0">
      {severityDot({ severity: alert.severity })}
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className={`font-medium ${alert.severity === "Error" ? "text-red-400" : "text-yellow-400"}`}>
            {alert.type}
          </span>
          <span className="text-muted-foreground truncate">{alert.component}</span>
        </div>
        <p className="text-muted-foreground leading-snug mt-0.5">{alert.message}</p>
      </div>
      <span className="shrink-0 text-muted-foreground tabular-nums">
        {formatRelativeTime(alert.timestamp)}
      </span>
    </div>
  );
}

export function AlertPanel() {
  const { alerts } = useRisk();

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          Active Alerts
        </CardTitle>
        {alerts.length > 0 ? (
          <Badge
            variant="outline"
            className="bg-red-500/20 text-red-400 border-red-500/30 tabular-nums"
          >
            {alerts.length}
          </Badge>
        ) : (
          <Badge
            variant="outline"
            className="bg-emerald-500/20 text-emerald-400 border-emerald-500/30"
          >
            All Clear
          </Badge>
        )}
      </CardHeader>
      <CardContent>
        {alerts.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No active alerts — all systems healthy.
          </p>
        ) : (
          <div className="space-y-0">
            {alerts.map((alert) => (
              <AlertRow key={`${alert.type}:${alert.component}`} alert={alert} />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
