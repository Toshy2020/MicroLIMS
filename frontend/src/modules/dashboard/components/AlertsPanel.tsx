import { useEffect, useState } from "react";
import { Paper, Box, Typography, Stack, useTheme } from "@mui/material";
import { Theme } from "@mui/material/styles";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import { NotificationItem } from "../types/dashboard";
import { SectionTitle } from "../../../components/SectionTitle";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { DashboardService } from "../services/DashboardService";

function severityIcon(theme: Theme): Record<string, { Icon: typeof ErrorOutlineIcon; color: string }> {
  return {
    error: { Icon: ErrorOutlineIcon, color: theme.custom.status.detected.text },
    warning: { Icon: WarningAmberOutlinedIcon, color: theme.custom.status.action.text },
    info: { Icon: InfoOutlinedIcon, color: theme.custom.status.info.text },
    success: { Icon: CheckCircleOutlineIcon, color: theme.custom.status.notDetected.text }
  };
}

function timeAgo(timestamp: string): string {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(timestamp).getTime()) / 60_000));
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hr${hours > 1 ? "s" : ""} ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

export function AlertsPanel() {
  const theme = useTheme();
  const icons = severityIcon(theme);
  const [notifications, setNotifications] = useState<NotificationItem[] | null>(null);

  useEffect(() => { DashboardService.getNotifications().then(setNotifications).catch(() => setNotifications([])); }, []);

  const handleClick = (n: NotificationItem) => {
    if (n.id == null || n.isRead) return;
    setNotifications((prev) => prev && prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    DashboardService.markNotificationRead(n.id).catch(() => {});
  };

  return (
    <Paper sx={{ p: 2.5 }}>
      <SectionTitle>Alerts &amp; Notifications</SectionTitle>
      {!notifications ? <LoadingSpinner /> : (
        <Stack spacing={1.25}>
          {notifications.slice(0, 8).map((n, i) => {
            const { Icon, color } = icons[n.severity] ?? icons.info;
            return (
              <Box
                key={n.id ?? i}
                onClick={() => handleClick(n)}
                sx={{ display: "flex", gap: 1.25, alignItems: "flex-start", cursor: n.isRead ? "default" : "pointer", opacity: n.isRead ? 0.6 : 1 }}
              >
                <Icon fontSize="small" sx={{ color, mt: 0.25, flexShrink: 0 }} />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{n.message}</Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{timeAgo(n.timestamp)}</Typography>
                </Box>
              </Box>
            );
          })}
          {notifications.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No alerts right now.</Typography>}
        </Stack>
      )}
    </Paper>
  );
}
