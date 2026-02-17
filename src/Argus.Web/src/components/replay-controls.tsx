"use client";

import { useEffect, useState } from "react";
import { useRisk } from "@/providers/risk-provider";
import { apiClient } from "@/lib/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Play, History, AlertCircle } from "lucide-react";
import type { AvailableRange, ReplaySpeed } from "@/types/replay";
import { REPLAY_SPEEDS } from "@/types/replay";

/**
 * Controls for starting a replay session.
 * Shows available time range, allows selecting start/end times and playback speed.
 */
export function ReplayControls() {
  const { isReplayMode, startReplay } = useRisk();
  const [availableRange, setAvailableRange] = useState<AvailableRange | null>(
    null
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [startTime, setStartTime] = useState("");
  const [endTime, setEndTime] = useState("");
  const [speed, setSpeed] = useState<ReplaySpeed>(1);
  const [submitting, setSubmitting] = useState(false);

  // Fetch available range on mount
  useEffect(() => {
    async function fetchRange() {
      try {
        const range = await apiClient.getAvailableRange();
        setAvailableRange(range);

        // Pre-fill form with last 5 minutes (or available range)
        if (range) {
          const latest = new Date(range.latest);
          const fiveMinutesAgo = new Date(latest.getTime() - 5 * 60 * 1000);
          const earliest = new Date(range.earliest);

          // Use the later of earliest or 5 minutes ago
          const startDate = fiveMinutesAgo > earliest ? fiveMinutesAgo : earliest;

          setStartTime(toLocalDateTimeString(startDate));
          setEndTime(toLocalDateTimeString(latest));
        }
      } catch {
        setError("Failed to fetch available range");
      } finally {
        setLoading(false);
      }
    }

    fetchRange();
  }, []);

  const handleStartReplay = async () => {
    if (!startTime || !endTime) return;

    setSubmitting(true);
    setError(null);

    try {
      await startReplay(new Date(startTime), new Date(endTime), speed);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start replay");
    } finally {
      setSubmitting(false);
    }
  };

  // Don't show controls during active replay
  if (isReplayMode) {
    return null;
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <History className="h-4 w-4" />
          Historical Replay
        </CardTitle>
      </CardHeader>
      <CardContent>
        {loading ? (
          <div className="text-sm text-muted-foreground">
            Loading available data range...
          </div>
        ) : !availableRange ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <AlertCircle className="h-4 w-4" />
            No historical data available for replay
          </div>
        ) : (
          <div className="space-y-4">
            {/* Available range info */}
            <div className="text-xs text-muted-foreground">
              Data available:{" "}
              {new Date(availableRange.earliest).toLocaleString()} —{" "}
              {new Date(availableRange.latest).toLocaleString()}
            </div>

            {/* Time range inputs */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="start-time" className="text-xs">
                  Start Time
                </Label>
                <input
                  id="start-time"
                  type="datetime-local"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="end-time" className="text-xs">
                  End Time
                </Label>
                <input
                  id="end-time"
                  type="datetime-local"
                  value={endTime}
                  onChange={(e) => setEndTime(e.target.value)}
                  className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </div>
            </div>

            {/* Speed and play button */}
            <div className="flex items-end gap-4">
              <div className="space-y-1.5">
                <Label className="text-xs">Playback Speed</Label>
                <Select
                  value={String(speed)}
                  onValueChange={(v) => setSpeed(Number(v) as ReplaySpeed)}
                >
                  <SelectTrigger className="w-24">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REPLAY_SPEEDS.map((s) => (
                      <SelectItem key={s} value={String(s)}>
                        {s}x
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <Button
                onClick={handleStartReplay}
                disabled={submitting || !startTime || !endTime}
                className="gap-2"
              >
                <Play className="h-4 w-4" />
                {submitting ? "Starting..." : "Start Replay"}
              </Button>
            </div>

            {/* Error message */}
            {error && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle className="h-4 w-4" />
                {error}
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Convert a Date to local datetime-local input format (YYYY-MM-DDTHH:mm)
 */
function toLocalDateTimeString(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
