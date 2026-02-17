"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import type { RiskSnapshot } from "@/types/domain";
import type { ReplaySpeed, ReplayState } from "@/types/replay";
import { apiClient } from "@/lib/api-client";

export type ConnectionStatus =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

export interface PnlDataPoint {
  timestamp: number; // epoch ms
  value: number; // net P&L USD
}

const MAX_HISTORY = 300;

interface RiskContextValue {
  snapshot: RiskSnapshot | null;
  connectionStatus: ConnectionStatus;
  pnlHistory: PnlDataPoint[];
  // Replay state
  isReplayMode: boolean;
  replayStatus: ReplayState | null;
  startReplay: (start: Date, end: Date, speed: ReplaySpeed) => Promise<void>;
  stopReplay: () => Promise<void>;
  pauseReplay: () => Promise<void>;
  resumeReplay: () => Promise<void>;
}

const RiskContext = createContext<RiskContextValue>({
  snapshot: null,
  connectionStatus: "disconnected",
  pnlHistory: [],
  isReplayMode: false,
  replayStatus: null,
  startReplay: async () => {},
  stopReplay: async () => {},
  pauseReplay: async () => {},
  resumeReplay: async () => {},
});

export function useRisk() {
  return useContext(RiskContext);
}

/**
 * Append a new P&L data point to the history buffer, keeping at most MAX_HISTORY entries.
 *
 * Trade-off: We use a simple array with slice() rather than a ring buffer.
 * For 300 entries at 1Hz this is perfectly fast — the GC cost of the discarded
 * array is negligible. A ring buffer would avoid allocations but adds complexity
 * (modular indexing, converting to ordered array for Recharts).
 */
function appendToHistory(
  history: PnlDataPoint[],
  point: PnlDataPoint
): PnlDataPoint[] {
  const next = [...history, point];
  return next.length > MAX_HISTORY ? next.slice(next.length - MAX_HISTORY) : next;
}

const SIGNALR_URL =
  process.env.NEXT_PUBLIC_SIGNALR_URL ?? "http://localhost:5000/hubs/risk";

// Exponential backoff delays: 0s → 1s → 2s → 5s → 10s → 30s cap
const RETRY_DELAYS = [0, 1000, 2000, 5000, 10000, 30000];

export function RiskProvider({ children }: { children: React.ReactNode }) {
  const [snapshot, setSnapshot] = useState<RiskSnapshot | null>(null);
  const [connectionStatus, setConnectionStatus] =
    useState<ConnectionStatus>("disconnected");
  const [pnlHistory, setPnlHistory] = useState<PnlDataPoint[]>([]);

  // Replay state
  const [isReplayMode, setIsReplayMode] = useState(false);
  const [replayStatus, setReplayStatus] = useState<ReplayState | null>(null);

  // Ref to track retry count across reconnection attempts
  const retryCount = useRef(0);
  const connectionRef = useRef<HubConnection | null>(null);

  const connect = useCallback(async () => {
    // Clean up existing connection
    if (connectionRef.current) {
      await connectionRef.current.stop();
    }

    const connection = new HubConnectionBuilder()
      .withUrl(SIGNALR_URL)
      .withAutomaticReconnect(RETRY_DELAYS)
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Handle incoming risk snapshots (live data from RiskSnapshotConsumerWorker)
    connection.on("RiskUpdated", (data: RiskSnapshot) => {
      // Only update if NOT in replay mode
      if (!isReplayMode) {
        setSnapshot(data);
        setPnlHistory((prev) =>
          appendToHistory(prev, {
            timestamp: Date.now(),
            value: data.totalNetPnlUsd,
          })
        );
      }
    });

    // Handle replay snapshots
    connection.on("ReplayUpdate", (data: RiskSnapshot) => {
      setSnapshot(data);
      setPnlHistory((prev) =>
        appendToHistory(prev, {
          timestamp: new Date(data.timestamp).getTime(),
          value: data.totalNetPnlUsd,
        })
      );
    });

    // Handle replay status changes
    connection.on("ReplayStatus", (status: ReplayState) => {
      setIsReplayMode(status.isActive);
      setReplayStatus(status);

      // Clear P&L history when starting replay
      if (status.isActive && !replayStatus?.isActive) {
        setPnlHistory([]);
      }
    });

    // Connection lifecycle events
    connection.onreconnecting(() => {
      setConnectionStatus("reconnecting");
    });

    connection.onreconnected(() => {
      retryCount.current = 0;
      setConnectionStatus("connected");
    });

    connection.onclose(() => {
      setConnectionStatus("disconnected");
      // Manual reconnect after automatic retries exhausted
      const delay =
        RETRY_DELAYS[
          Math.min(retryCount.current, RETRY_DELAYS.length - 1)
        ];
      retryCount.current++;
      setTimeout(() => {
        connect();
      }, delay);
    });

    // Start connection
    setConnectionStatus("connecting");
    try {
      await connection.start();
      retryCount.current = 0;
      setConnectionStatus("connected");
    } catch {
      setConnectionStatus("disconnected");
      // Will retry via onclose handler
    }
  }, [isReplayMode, replayStatus?.isActive]);

  useEffect(() => {
    connect();
    return () => {
      connectionRef.current?.stop();
    };
  }, [connect]);

  // Replay actions
  const startReplay = useCallback(async (start: Date, end: Date, speed: ReplaySpeed) => {
    await apiClient.startReplay(start.toISOString(), end.toISOString(), speed);
  }, []);

  const stopReplay = useCallback(async () => {
    await apiClient.stopReplay();
  }, []);

  const pauseReplay = useCallback(async () => {
    await apiClient.pauseReplay();
  }, []);

  const resumeReplay = useCallback(async () => {
    await apiClient.resumeReplay();
  }, []);

  return (
    <RiskContext.Provider
      value={{
        snapshot,
        connectionStatus,
        pnlHistory,
        isReplayMode,
        replayStatus,
        startReplay,
        stopReplay,
        pauseReplay,
        resumeReplay,
      }}
    >
      {children}
    </RiskContext.Provider>
  );
}
