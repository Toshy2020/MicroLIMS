import { useEffect, useState } from "react";
import { isNetworkError } from "../services/networkError";
import { NETWORK_FAILURE_EVENT } from "../services/connectionSignal";
import { healthService } from "../services/healthService";

const CHECK_INTERVAL_MS = 15_000;

// Whether the MicroLIMS server can be reached. Offline when a request or the
// health check fails with no HTTP answer; online again after one successful
// health check. An HTTP error status (a 500) is still an answer, so it is not
// offline. Polls GET /health every 15 s while `active` (a bench form is open or
// drafts exist) and, whatever `active` says, while offline so recovery is noticed.
export function useConnectionStatus(active: boolean): { online: boolean; lastCheckedAt: Date | null } {
  const [online, setOnline] = useState(true);
  const [lastCheckedAt, setLastCheckedAt] = useState<Date | null>(null);

  useEffect(() => {
    const goOffline = () => setOnline(false);
    window.addEventListener(NETWORK_FAILURE_EVENT, goOffline);
    window.addEventListener("offline", goOffline);
    return () => {
      window.removeEventListener(NETWORK_FAILURE_EVENT, goOffline);
      window.removeEventListener("offline", goOffline);
    };
  }, []);

  const polling = active || !online;
  useEffect(() => {
    if (!polling) return;
    let controller: AbortController | null = null;

    const check = async () => {
      controller?.abort();
      const current = new AbortController();
      controller = current;
      try {
        await healthService.check(current.signal);
        if (current.signal.aborted) return;
        setOnline(true);
      } catch (error) {
        if (current.signal.aborted) return;
        // A server that answers with an error status is reachable.
        setOnline(!isNetworkError(error));
      }
      setLastCheckedAt(new Date());
    };

    const interval = setInterval(() => void check(), CHECK_INTERVAL_MS);
    const onBrowserOnline = () => void check();
    window.addEventListener("online", onBrowserOnline);
    return () => {
      clearInterval(interval);
      window.removeEventListener("online", onBrowserOnline);
      controller?.abort();
    };
  }, [polling]);

  return { online, lastCheckedAt };
}
