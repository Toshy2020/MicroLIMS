import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box, Typography, Avatar, IconButton, Badge, Menu, MenuItem, Divider,
  ListItemIcon, ListItemText, Tooltip, Switch, useTheme, useMediaQuery
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import NotificationsIcon from "@mui/icons-material/Notifications";
import PersonIcon from "@mui/icons-material/Person";
import LockResetIcon from "@mui/icons-material/LockReset";
import LogoutIcon from "@mui/icons-material/Logout";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import { useAuth } from "../contexts/AuthContext";
import { apiClient } from "../services/apiClient";
import { brandColors } from "../theme";
import { useThemeMode } from "../theme/ThemeModeContext";

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
// Note: Review and Approval queues live inside Testing Workspace.
const NOTIFICATION_ROUTES: Record<string, string> = {
  MediaExpiry: "/laboratory-configuration/media",
  IncubationReady: "/testing-workspace",
  ApprovalWaiting: "/testing-workspace",
  ReviewWaiting: "/testing-workspace"
};

const POLL_INTERVAL_MS = 60_000;

function formatRoleFallback(role: string | null): string {
  if (!role) return "Staff";
  switch (role) {
    case "Analyst": return "Microbiology Analyst";
    case "SectionHead": return "Section Head";
    case "Reviewer": return "Quality Reviewer";
    case "SystemAdministrator": return "System Administrator";
    default: return role;
  }
}

interface HeaderProps {
  onToggleSidebar?: () => void;
  sidebarCollapsed?: boolean;
}

// Purple-gradient brand topbar with responsive identity and sidebar toggle
export function Header({ onToggleSidebar, sidebarCollapsed }: HeaderProps) {
  const { username, fullName, jobTitle, role, logout } = useAuth();
  const { mode, toggleMode } = useThemeMode();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const navigate = useNavigate();
  const initial = (fullName ?? username ?? "U").charAt(0).toUpperCase();
  const displayTitle = jobTitle?.trim() || formatRoleFallback(role);

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

        <Box
          onClick={(e) => setAccountAnchor(e.currentTarget)}
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1.25,
            cursor: "pointer",
            p: 0.5,
            borderRadius: 1.5,
            "&:hover": { bgcolor: "rgba(255, 255, 255, 0.08)" }
          }}
        >
          <Avatar sx={{ width: 34, height: 34, bgcolor: "#fff", color: brandColors.sectionTitle, fontWeight: 700, fontSize: 14 }}>
            {initial}
          </Avatar>
          <Box sx={{ display: { xs: "none", sm: "block" }, textAlign: "left", lineHeight: 1.2 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, color: "#fff", lineHeight: 1.1 }}>
              {fullName ?? username}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "rgba(255, 255, 255, 0.8)", lineHeight: 1.1 }}>
              {displayTitle}
            </Typography>
          </Box>
        </Box>

        <Menu anchorEl={accountAnchor} open={Boolean(accountAnchor)} onClose={() => setAccountAnchor(null)} PaperProps={{ sx: { width: 250 } }}>
          <Box sx={{ px: 2, py: 1.25 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{fullName ?? username}</Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 500 }}>{displayTitle}</Typography>
            <Typography sx={{ fontSize: 11, color: "text.disabled" }}>Role: {role}</Typography>
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
