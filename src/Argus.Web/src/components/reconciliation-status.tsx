"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { apiClient } from "@/lib/api-client";
import type { ReconciliationReport } from "@/types/domain";

export function ReconciliationStatus() {
  const [report, setReport] = useState<ReconciliationReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const runReconciliation = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await apiClient.runReconciliation();
      setReport(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Reconciliation failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          Reconciliation
        </CardTitle>
        <div className="flex items-center gap-2">
          {report && <StatusBadge passed={report.passed} />}
          {!report && !loading && (
            <Badge variant="outline" className="text-muted-foreground">
              Never run
            </Badge>
          )}
          <button
            onClick={runReconciliation}
            disabled={loading}
            className="rounded-md bg-primary px-3 py-1 text-xs font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-50"
          >
            {loading ? "Running..." : "Run"}
          </button>
        </div>
      </CardHeader>
      <CardContent>
        {error && (
          <p className="text-sm text-red-400">{error}</p>
        )}
        {report && <ReportDetails report={report} />}
        {!report && !error && (
          <p className="text-sm text-muted-foreground">
            Replay all events and verify position state matches.
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function StatusBadge({ passed }: { passed: boolean }) {
  return passed ? (
    <Badge
      variant="outline"
      className="bg-emerald-500/20 text-emerald-400 border-emerald-500/30"
    >
      Pass
    </Badge>
  ) : (
    <Badge
      variant="outline"
      className="bg-red-500/20 text-red-400 border-red-500/30"
    >
      Fail
    </Badge>
  );
}

function ReportDetails({ report }: { report: ReconciliationReport }) {
  return (
    <div className="space-y-2">
      <div className="flex gap-4 text-sm">
        <span className="text-muted-foreground">
          Events: <span className="text-foreground tabular-nums">{report.totalEventsReplayed}</span>
        </span>
        <span className="text-muted-foreground">
          Checksum:{" "}
          <span
            className={`font-mono text-xs ${
              report.expectedChecksum === report.actualChecksum
                ? "text-emerald-400"
                : "text-red-400"
            }`}
          >
            {report.expectedChecksum.slice(0, 12)}...
          </span>
        </span>
      </div>

      {report.discrepancies.length > 0 && (
        <div className="mt-2 rounded-md border border-red-500/30 bg-red-500/5 p-3">
          <p className="mb-2 text-xs font-medium text-red-400">
            {report.discrepancies.length} discrepanc{report.discrepancies.length === 1 ? "y" : "ies"} found
          </p>
          <div className="space-y-1">
            {report.discrepancies.map((d, i) => (
              <div key={i} className="flex items-center gap-2 text-xs">
                <span className="font-medium text-foreground">{d.symbol}</span>
                <span className="text-muted-foreground">{d.field}</span>
                <span className="text-red-400">
                  {d.expected} → {d.actual}
                </span>
                {d.difference !== null && (
                  <span className="font-mono text-muted-foreground">
                    ({d.difference > 0 ? "+" : ""}{d.difference})
                  </span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
