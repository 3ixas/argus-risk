"use client";

import { Badge } from "@/components/ui/badge";
import { useRisk } from "@/providers/risk-provider";

const qualityConfig: Record<string, { className: string }> = {
  Good: { className: "bg-emerald-500/20 text-emerald-400 border-emerald-500/30" },
  Degraded: { className: "bg-yellow-500/20 text-yellow-400 border-yellow-500/30" },
  Stale: { className: "bg-red-500/20 text-red-400 border-red-500/30" },
};

export function DataQualityIndicator() {
  const { snapshot } = useRisk();

  if (!snapshot) return null;

  const quality = snapshot.dataQuality ?? "Good";
  const config = qualityConfig[quality] ?? qualityConfig.Good;

  return (
    <Badge variant="outline" className={config.className}>
      {quality}
    </Badge>
  );
}
