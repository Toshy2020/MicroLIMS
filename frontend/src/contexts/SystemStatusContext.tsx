import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { MAINTENANCE_EVENT, MaintenanceRefusalDetail } from "../services/maintenanceSignal";
import { NO_STATUS, SystemStatus, SystemStatusService } from "../services/systemStatusService";

const POLL_INTERVAL_MS = 60_000;
const MAINTENANCE_TOAST_ID = "maintenance-refusal";

interface SystemStatusContextValue {
  status: SystemStatus;
  refresh: () => void;
}

const SystemStatusContext = createContext<SystemStatusContextValue>({ status: NO_STATUS, refresh: () => undefined });

export function useSystemStatus(): SystemStatusContextValue {
  return useContext(SystemStatusContext);
}

// Keeps the maintenance flag and announcement current: polled, refreshed when
// the tab regains focus, and refreshed at once when the API refuses a write
// because maintenance mode is on (that refusal also shows the server's message
// instead of a generic error).
export function SystemStatusProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<SystemStatus>(NO_STATUS);

  const refresh = useCallback(() => {
    SystemStatusService.get().then(setStatus).catch(() => {
      // The status is advisory: an unreachable server shows its own errors elsewhere.
    });
  }, []);

  useEffect(() => {
    refresh();
    const timer = window.setInterval(refresh, POLL_INTERVAL_MS);
    const onFocus = () => refresh();
    const onRefusal = (event: Event) => {
      const message = (event as CustomEvent<MaintenanceRefusalDetail>).detail?.message;
      // Same id: repeated refused saves update one message instead of stacking.
      toast.warning(message || "MicroLIMS is in maintenance mode. Your change was not saved.", {
        id: MAINTENANCE_TOAST_ID, duration: 10_000
      });
      refresh();
    };
    window.addEventListener("focus", onFocus);
    window.addEventListener(MAINTENANCE_EVENT, onRefusal);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", onFocus);
      window.removeEventListener(MAINTENANCE_EVENT, onRefusal);
    };
  }, [refresh]);

  const value = useMemo(() => ({ status, refresh }), [status, refresh]);
  return <SystemStatusContext.Provider value={value}>{children}</SystemStatusContext.Provider>;
}
