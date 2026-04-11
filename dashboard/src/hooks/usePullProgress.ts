
import { createContext, useContext } from "react";

export interface PullProgress {
  modelName: string;
  downloaded: number;
  total: number;
  status: string;
}

export interface PullProgressContextType {
  activePulls: Record<string, PullProgress>;
  startPull: (modelName: string) => void;
  cancelPull: (modelName: string) => void;
}

export const PullProgressContext = createContext<PullProgressContextType>({
  activePulls: {},
  startPull: () => {},
  cancelPull: () => {},
});

export const usePullProgress = () => {
  return useContext(PullProgressContext);
};
