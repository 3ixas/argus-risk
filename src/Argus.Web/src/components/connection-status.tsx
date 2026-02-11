"use client";

import { Badge } from "@/components/ui/badge";
import { useRisk } from "@/providers/risk-provider";

const statusConfig = {
  connected: { label: "Live", className: "bg-emerald-500/20 text-emerald-400 border-emerald-500/30" },
  connecting: { label: "Connecting", className: "bg-yellow-500/20 text-yellow-400 border-yellow-500/30" },
  reconnecting: { label: "Reconnecting", className: "bg-yellow-500/20 text-yellow-400 border-yellow-500/30" },
  disconnected: { label: "Disconnected", className: "bg-red-500/20 text-red-400 border-red-500/30" },
} as const;

export function ConnectionStatus() {
  const { connectionStatus } = useRisk();
  const config = statusConfig[connectionStatus];

  return (
    <Badge variant="outline" className={config.className}>
      <span className={`mr-1.5 inline-block h-2 w-2 rounded-full ${
        connectionStatus === "connected" ? "bg-emerald-400 animate-pulse" :
        connectionStatus === "disconnected" ? "bg-red-400" :
        "bg-yellow-400 animate-pulse"
      }`} />
      {config.label}
    </Badge>
  );
}
