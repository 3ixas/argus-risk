// Replay session types matching C# models in Argus.Api.Replay

export interface ReplayState {
  isActive: boolean;
  isPaused: boolean;
  startTime: string;
  endTime: string;
  currentTime: string;
  speed: number;
}

export interface AvailableRange {
  earliest: string;
  latest: string;
}

export type ReplaySpeed = 1 | 5 | 10 | 60;

export const REPLAY_SPEEDS: ReplaySpeed[] = [1, 5, 10, 60];
