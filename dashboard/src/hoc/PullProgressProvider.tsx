
import React, { useCallback, useRef, useState } from "react";
import { useDispatch } from "react-redux";
import toast from "react-hot-toast";
import {
  PullProgress,
  PullProgressContext,
} from "../hooks/usePullProgress";
import { axiosInstance } from "../lib/store/rtk/rtkApiInstance";
import apiSlice from "../lib/store/rtk/rtkApiInstance";

const PullProgressProvider = ({ children }: { children: React.ReactNode }) => {
  const dispatch = useDispatch();
  const [activePulls, setActivePulls] = useState<Record<string, PullProgress>>(
    {}
  );
  const abortControllers = useRef<Record<string, AbortController>>({});

  const refetchLocalModels = useCallback(() => {
    // Force an immediate refetch of the local models list so the
    // Models table updates after a pull completes.
    dispatch(
      (apiSlice as any).endpoints.getLocalModels.initiate(undefined, {
        forceRefetch: true,
      })
    );
  }, [dispatch]);

  const startPull = useCallback(
    (modelName: string) => {
      const controller = new AbortController();
      abortControllers.current[modelName] = controller;

      setActivePulls((prev) => ({
        ...prev,
        [modelName]: {
          modelName,
          downloaded: 0,
          total: 0,
          status: "starting",
        },
      }));

      axiosInstance({
        url: "/api/pull",
        method: "POST",
        data: { model: modelName },
        signal: controller.signal,
        onDownloadProgress: (pe) => {
          let status = "downloading";
          let downloaded = pe.loaded ?? 0;
          let total = pe.total ?? 0;

          try {
            const responseText =
              (pe.event?.currentTarget as XMLHttpRequest)?.responseText ??
              (pe.event?.target as XMLHttpRequest)?.responseText;
            if (responseText) {
              const lastBrace = responseText.lastIndexOf("}");
              if (lastBrace >= 0) {
                const lastOpen = responseText.lastIndexOf("{", lastBrace);
                if (lastOpen >= 0) {
                  const lastObj = JSON.parse(
                    responseText.substring(lastOpen, lastBrace + 1)
                  );
                  if (lastObj.status) status = lastObj.status;
                  if (lastObj.downloaded != null)
                    downloaded = lastObj.downloaded;
                  else if (lastObj.completed != null)
                    downloaded = lastObj.completed;
                  if (lastObj.total != null && lastObj.total > 0)
                    total = lastObj.total;
                  if (
                    (!total || total === 0) &&
                    lastObj.percent != null &&
                    lastObj.percent > 0 &&
                    downloaded > 0
                  ) {
                    total = Math.round(downloaded / Number(lastObj.percent));
                  }
                }
              }
            }
          } catch {
            // ignore parse errors — use axios-level progress
          }

          setActivePulls((prev) => ({
            ...prev,
            [modelName]: { modelName, downloaded, total, status },
          }));
        },
      })
        .then(() => {
          delete abortControllers.current[modelName];
          setActivePulls((prev) => {
            const { [modelName]: _removed, ...rest } = prev;
            return rest;
          });
          toast.success(`Successfully pulled model: ${modelName}`);
          refetchLocalModels();
        })
        .catch((error) => {
          delete abortControllers.current[modelName];
          setActivePulls((prev) => {
            const { [modelName]: _removed, ...rest } = prev;
            return rest;
          });
          if (controller.signal.aborted) {
            toast(`Cancelled pulling model: ${modelName}`);
          } else {
            toast.error(
              `Failed to pull model: ${
                error?.response?.data?.error ||
                error?.message ||
                "Unknown error"
              }`
            );
          }
        });
    },
    [refetchLocalModels]
  );

  const cancelPull = useCallback((modelName: string) => {
    const controller = abortControllers.current[modelName];
    if (controller) {
      controller.abort();
    }
  }, []);

  return (
    <PullProgressContext.Provider
      value={{ activePulls, startPull, cancelPull }}
    >
      {children}
    </PullProgressContext.Provider>
  );
};

export default PullProgressProvider;
