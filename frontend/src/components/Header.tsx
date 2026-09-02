import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import {
  Box, Typography, IconButton, Badge, Menu, MenuItem, Divider,
  ListItemText, Tooltip, Switch, useTheme, useMediaQuery, Button
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import NotificationsIcon from "@mui/icons-material/Notifications";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import { apiClient } from "../services/apiClient";
import { useThemeMode } from "../theme/ThemeModeContext";

interface NotificationDto {
  id: number | null;
  type: string;
  message: string;
  timestamp: string;
  severity: string;
  isRead: boolean;
}

// Where clicking a notification should take the user
const NOTIFICATION_ROUTES: Record<string, string> = {
  MediaExpiry: "/laboratory-configuration/media",
  IncubationReady: "/testing-workspace",
  ApprovalWaiting: "/testing-workspace",
  ReviewWaiting: "/testing-workspace",
  TestReturnedForRevision: "/receiving-testing",
  DiscussionComment: "/discussions",
  DiscussionPostUpdated: "/discussions",
  DirectMessage: "/messages"
};

const POLL_INTERVAL_MS = 60_000;

interface HeaderProps {
  onToggleSidebar?: () => void;
  sidebarCollapsed?: boolean;
}

export function Header({ onToggleSidebar, sidebarCollapsed }: HeaderProps) {
  const { mode, toggleMode } = useThemeMode();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const navigate = useNavigate();

  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [bellAnchor, setBellAnchor] = useState<HTMLElement | null>(null);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const loadNotifications = () => {
    apiClient.get("/dashboard/notifications").then((r) => setNotifications(r.data.data)).catch(() => {});
  };

  useEffect(() => {
    loadNotifications();
    const interval = setInterval(loadNotifications, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  const handleNotificationClick = (notification: NotificationDto) => {
    setBellAnchor(null);
    if (notification.id !== null) {
      apiClient.post(`/dashboard/notifications/${notification.id}/read`).catch(() => {});
      setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)));
    }
    const target = NOTIFICATION_ROUTES[notification.type];
    if (target) navigate(target);
  };

  const handleMarkAllRead = () => {
    if (unreadCount === 0) return;
    apiClient.post("/dashboard/notifications/read-all").catch(() => {});
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  };

  return (
    <Box
      component="header"
      className="no-print"
      sx={{
        background: theme.custom.chrome.topbarBg,
        color: "#fff",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        px: { xs: 1.5, sm: 3 },
        height: 56,
        minHeight: 56,
        maxHeight: 56,
        flexShrink: 0,
        boxShadow: "0 2px 4px rgba(0,0,0,0.12)",
        position: "relative",
        zIndex: 1100
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {onToggleSidebar && (
          <Tooltip title={isMobile ? "Toggle navigation menu" : (sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar")}>
            <IconButton
              onClick={onToggleSidebar}
              sx={{ color: "#fff", p: 0.75, mr: 0.5 }}
              aria-label="Toggle navigation menu"
            >
              <MenuIcon />
            </IconButton>
          </Tooltip>
        )}
        <Typography sx={{ fontSize: { xs: 19, sm: 22 }, fontWeight: 700, letterSpacing: 0.5, userSelect: "none" }}>
          Micro<Box component="span" sx={{ fontWeight: 300, color: theme.custom.chrome.brandAccent }}>LIMS</Box>
        </Typography>
      </Box>

      <Box sx={{ display: "flex", alignItems: "center", gap: { xs: 1, sm: 1.5 } }}>
        <Tooltip title={mode === "dark" ? "Switch to light mode" : "Switch to dark mode"}>
          <Switch
            checked={mode === "dark"}
            onChange={toggleMode}
            icon={<LightModeIcon sx={{ fontSize: 15, color: "#f2b705", p: "1.5px" }} />}
            checkedIcon={<DarkModeIcon sx={{ fontSize: 15, color: "#2E3542", p: "1.5px" }} />}
            sx={{
              "& .MuiSwitch-track": { backgroundColor: "rgba(255,255,255,0.28)", opacity: 1 },
              "& .MuiSwitch-thumb": { backgroundColor: "#fff" },
              "& .Mui-checked+.MuiSwitch-track": { backgroundColor: "rgba(255,255,255,0.28) !important", opacity: 1 }
            }}
          />
        </Tooltip>
        <Tooltip title="Notifications">
          <IconButton onClick={(e) => setBellAnchor(e.currentTarget)} sx={{ color: "#fff" }}>
            <Badge badgeContent={unreadCount} color="error">
              <NotificationsIcon />
            </Badge>
          </IconButton>
        </Tooltip>
        <Menu anchorEl={bellAnchor} open={Boolean(bellAnchor)} onClose={() => setBellAnchor(null)} PaperProps={{ sx: { width: 360, maxHeight: 420 } }}>
          {notifications.length > 0 && (
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", px: 2, py: 0.75 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Notifications</Typography>
              <Button
                size="small"
                onClick={handleMarkAllRead}
                disabled={unreadCount === 0}
                sx={{ fontSize: 11, textTransform: "none", minWidth: 0 }}
              >
                Mark all as read
              </Button>
            </Box>
          )}
          {notifications.length > 0 && <Divider />}
          {notifications.length === 0 && (
            <MenuItem disabled>
              <ListItemText primary="Nothing pending." />
            </MenuItem>
          )}
          {notifications.map((n, i) => {
            const target = NOTIFICATION_ROUTES[n.type];
            return (
              <MenuItem
                key={n.id ?? i}
                {...(target ? { component: Link, to: target } : {})}
                onClick={() => handleNotificationClick(n)}
                sx={{ whiteSpace: "normal", alignItems: "flex-start" }}
              >
                <ListItemText
                  primary={n.message}
                  secondary={new Date(n.timestamp).toLocaleString()}
                  primaryTypographyProps={{ fontWeight: n.isRead ? 400 : 700, fontSize: 13 }}
                  secondaryTypographyProps={{ fontSize: 11 }}
                />
              </MenuItem>
            );
          })}
        </Menu>
      </Box>
    </Box>
  );
}
