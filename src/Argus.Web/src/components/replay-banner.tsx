"use client";

import { useRisk } from "@/providers/risk-provider";
import { Button } from "@/components/ui/button";
import { Pause, Play, Square } from "lucide-react";

/**
 * Amber banner that appears at the top of the dashboard during replay mode.
 * Shows current replay time, speed, and provides pause/resume/stop controls.
 */
export function ReplayBanner() {
  const { isReplayMode, replayStatus, stopReplay, pauseReplay, resumeReplay } =
    useRisk();

  if (!isReplayMode || !replayStatus) {
    return null;
  }

  const currentTime = new Date(replayStatus.currentTime);
  const formattedTime = currentTime.toLocaleString("en-US", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });

  return (
    <div className="sticky top-0 z-50 flex items-center justify-between bg-amber-500/90 px-4 py-2 text-amber-950 backdrop-blur-sm">
      <div className="flex items-center gap-4">
        <span className="font-semibold uppercase tracking-wide">
          Replay Mode
        </span>
        <span className="font-mono text-sm">{formattedTime}</span>
        <span className="rounded bg-amber-600/50 px-2 py-0.5 text-xs font-medium">
          {replayStatus.speed}x
        </span>
        {replayStatus.isPaused && (
          <span className="rounded bg-amber-700/50 px-2 py-0.5 text-xs font-medium">
            PAUSED
          </span>
        )}
      </div>

      <div className="flex items-center gap-2">
        {replayStatus.isPaused ? (
          <Button
            variant="ghost"
            size="sm"
            onClick={resumeReplay}
            className="h-7 text-amber-950 hover:bg-amber-600/50 hover:text-amber-950"
          >
            <Play className="mr-1 h-4 w-4" />
            Resume
          </Button>
        ) : (
          <Button
            variant="ghost"
            size="sm"
            onClick={pauseReplay}
            className="h-7 text-amber-950 hover:bg-amber-600/50 hover:text-amber-950"
          >
            <Pause className="mr-1 h-4 w-4" />
            Pause
          </Button>
        )}
        <Button
          variant="ghost"
          size="sm"
          onClick={stopReplay}
          className="h-7 text-amber-950 hover:bg-amber-600/50 hover:text-amber-950"
        >
          <Square className="mr-1 h-4 w-4" />
          Stop
        </Button>
      </div>
    </div>
  );
}
