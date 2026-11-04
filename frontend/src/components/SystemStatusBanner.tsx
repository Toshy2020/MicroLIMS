import { useEffect, useRef } from "react";
import { Alert, AlertTitle, Box } from "@mui/material";
import { useSystemStatus } from "../contexts/SystemStatusContext";

// Presentational: also used by the Settings page to preview an announcement.
export function AnnouncementAlert({ text, severity }: { text: string; severity: "info" | "warning" }) {
  return (
    <Alert severity={severity} variant="filled" square role="status" aria-label="Announcement" sx={{ borderRadius: 0, width: "100%" }}>
      {text}
    </Alert>
  );
}

export function MaintenanceNotice({ message }: { message: string }) {
  return (
    <Alert severity="warning" variant="filled" square role="alert" aria-label="Maintenance mode" sx={{ borderRadius: 0, width: "100%" }}>
      <AlertTitle sx={{ mb: message ? 0.5 : 0 }}>Maintenance mode</AlertTitle>
      {message}
    </Alert>
  );
}

// Full-width strip above every page, sign-in included: the maintenance notice
// (writes are being refused) and the announcement. Publishes its height as
// --system-banner-height so the 100vh layouts below can subtract it.
export function SystemStatusBanner() {
  const { status } = useSystemStatus();
  const ref = useRef<HTMLDivElement>(null);
  const visible = status.maintenance || status.banner.trim().length > 0;

  useEffect(() => {
    const root = document.documentElement;
    const node = ref.current;
    if (!visible || !node) {
      root.style.removeProperty("--system-banner-height");
      return;
    }
    const publish = () => root.style.setProperty("--system-banner-height", `${node.offsetHeight}px`);
    publish();
    if (typeof ResizeObserver === "undefined") return () => root.style.removeProperty("--system-banner-height");
    const observer = new ResizeObserver(publish);
    observer.observe(node);
    return () => {
      observer.disconnect();
      root.style.removeProperty("--system-banner-height");
    };
  }, [visible, status.maintenance, status.banner, status.message]);

  if (!visible) return null;
  return (
    <Box ref={ref} sx={{ position: "sticky", top: 0, zIndex: (t) => t.zIndex.snackbar }}>
      {status.maintenance && <MaintenanceNotice message={status.message} />}
      {status.banner.trim() && <AnnouncementAlert text={status.banner} severity={status.severity} />}
    </Box>
  );
}
