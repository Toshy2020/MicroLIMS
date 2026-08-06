import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box, Typography, Avatar, IconButton, Badge, Menu, MenuItem, Divider,
  ListItemIcon, ListItemText, Tooltip
} from "@mui/material";
import NotificationsIcon from "@mui/icons-material/Notifications";
import PersonIcon from "@mui/icons-material/Person";
import LockResetIcon from "@mui/icons-material/LockReset";
import LogoutIcon from "@mui/icons-material/Logout";
import { useAuth } from "../contexts/AuthContext";
import { apiClient } from "../services/apiClient";
import { brandColors } from "../theme";

interface NotificationDto {
  id: number | null;
  type: string;
  message: string;
  timestamp: string;
  severity: string;
  isRead: boolean;
}

// Where clicking a notification should take the user - mirrors the
// four notification types DashboardNotificationService computes.
const NOTIFICATION_ROUTES: Record<string, string> = {
  MediaExpiry: "/laboratory-configuration/media",
  IncubationReady: "/testing-workspace",
  ApprovalWaiting: "/approval",
  ReviewWaiting: "/review"
};

const POLL_INTERVAL_MS = 60_000;

// Purple-gradient brand topbar, per the provided design.
export function Header() {
  const { username, fullName, role, logout } = useAuth();
  const navigate = useNavigate();
  const initial = (username ?? "U").charAt(0).toUpperCase();

  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [bellAnchor, setBellAnchor] = useState<HTMLElement | null>(null);
  const [accountAnchor, setAccountAnchor] = useState<HTMLElement | null>(null);

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

  const handleSignOut = () => {
    setAccountAnchor(null);
    logout();
    navigate("/login");
  };

  return (
    <Box
      className="no-print"
      sx={{
        background: brandColors.topbarGradient,
        color: "#fff",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        px: 3,
        py: 1.5
      }}
    >
      <Typography sx={{ fontSize: 22, fontWeight: 700, letterSpacing: 0.5 }}>
        Micro<Box component="span" sx={{ fontWeight: 300, opacity: 0.85 }}>LIMS</Box>
      </Typography>

      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
        <Tooltip title="Notifications">
          <IconButton onClick={(e) => setBellAnchor(e.currentTarget)} sx={{ color: "#fff" }}>
            <Badge badgeContent={unreadCount} color="error">
              <NotificationsIcon />
            </Badge>
          </IconButton>
        </Tooltip>
        <Menu anchorEl={bellAnchor} open={Boolean(bellAnchor)} onClose={() => setBellAnchor(null)} PaperProps={{ sx: { width: 360, maxHeight: 420 } }}>
          {notifications.length === 0 && (
            <MenuItem disabled>
              <ListItemText primary="Nothing pending." />
            </MenuItem>
          )}
          {notifications.map((n, i) => (
            <MenuItem key={n.id ?? i} onClick={() => handleNotificationClick(n)} sx={{ whiteSpace: "normal", alignItems: "flex-start" }}>
              <ListItemText
                primary={n.message}
                secondary={new Date(n.timestamp).toLocaleString()}
                primaryTypographyProps={{ fontWeight: n.isRead ? 400 : 700, fontSize: 13 }}
                secondaryTypographyProps={{ fontSize: 11 }}
              />
            </MenuItem>
          ))}
        </Menu>

        <IconButton onClick={(e) => setAccountAnchor(e.currentTarget)} sx={{ p: 0 }}>
          <Avatar sx={{ width: 34, height: 34, bgcolor: "#fff", color: brandColors.sectionTitle, fontWeight: 700, fontSize: 14 }}>
            {initial}
          </Avatar>
        </IconButton>
        <Menu anchorEl={accountAnchor} open={Boolean(accountAnchor)} onClose={() => setAccountAnchor(null)} PaperProps={{ sx: { width: 240 } }}>
          <Box sx={{ px: 2, py: 1 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{fullName ?? username}</Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{role}</Typography>
          </Box>
          <Divider />
          <MenuItem onClick={() => { setAccountAnchor(null); navigate("/profile"); }}>
            <ListItemIcon><PersonIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary="My Profile" />
          </MenuItem>
          <MenuItem onClick={() => { setAccountAnchor(null); navigate("/change-password"); }}>
            <ListItemIcon><LockResetIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary="Change Password" />
          </MenuItem>
          <Divider />
          <MenuItem onClick={handleSignOut}>
            <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary="Sign Out" />
          </MenuItem>
        </Menu>
      </Box>
    </Box>
  );
}
