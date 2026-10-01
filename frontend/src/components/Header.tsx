import { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import {
  Box, Typography, IconButton, Badge, Menu, MenuItem, Divider, Avatar, ButtonBase,
  ListItemIcon, ListItemText, Tooltip, Switch, useTheme, useMediaQuery, Button
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import NotificationsIcon from "@mui/icons-material/Notifications";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";
import ForumOutlinedIcon from "@mui/icons-material/ForumOutlined";
import MailOutlineIcon from "@mui/icons-material/MailOutlined";
import LockResetIcon from "@mui/icons-material/LockReset";
import LogoutIcon from "@mui/icons-material/Logout";
import { apiClient } from "../services/apiClient";
import { useThemeMode } from "../theme/ThemeModeContext";
import { useAuth } from "../contexts/AuthContext";
import { formatRoleFallback, userInitials } from "../utils/userDisplay";

interface NotificationDto {
  id: number | null;
  type: string;
  message: string;
  timestamp: string;
  severity: string;
  isRead: boolean;
  sampleId?: number | null;
  testOrderId?: number | null;
}

// A notification about one test on one sample opens that sample (and test)
// in the workspace; everything else goes to its type's page.
function notificationTarget(notification: NotificationDto): string | undefined {
  if (notification.sampleId) {
    const testPart = notification.testOrderId ? `&testOrderId=${notification.testOrderId}` : "";
    return `/receiving-testing?sampleId=${notification.sampleId}${testPart}`;
  }
  return NOTIFICATION_ROUTES[notification.type];
}

// Where clicking a notification should take the user when it is not about a
// specific sample
const NOTIFICATION_ROUTES: Record<string, string> = {
  MediaExpiry: "/laboratory-configuration/media",
  IncubationReady: "/receiving-testing",
  ApprovalWaiting: "/receiving-testing?status=UnderApproval",
  ReviewWaiting: "/receiving-testing?status=UnderReview",
  TestReturnedForRevision: "/receiving-testing",
  TestReturnedForBiochemical: "/receiving-testing",
  DiscussionComment: "/discussions",
  DiscussionPostUpdated: "/discussions",
  DirectMessage: "/messages"
};

const POLL_INTERVAL_MS = 60_000;
const FIRST_LOAD_DELAY_MS = 5_000;

interface HeaderProps {
  onToggleSidebar?: () => void;
  sidebarCollapsed?: boolean;
  // Lab membership, for the job-title fallback ("Microbiology Analyst").
  labCodes: string[];
}

const ROLE_LABELS: Record<string, string> = {
  SectionHead: "Section Head",
  SystemAdministrator: "System Administrator"
};

export function Header({ onToggleSidebar, sidebarCollapsed, labCodes }: HeaderProps) {
  const { mode, toggleMode } = useThemeMode();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const showIdentityText = useMediaQuery(theme.breakpoints.up("sm"));
  const navigate = useNavigate();
  const { username, fullName, jobTitle, role, logout } = useAuth();

  const displayName = fullName ?? username ?? "User";
  const displayTitle = jobTitle?.trim() || formatRoleFallback(role, labCodes);

  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [bellAnchor, setBellAnchor] = useState<HTMLElement | null>(null);
  const [profileAnchor, setProfileAnchor] = useState<HTMLElement | null>(null);
  const [unreadMessages, setUnreadMessages] = useState<number>(0);

  const loadUnreadMessages = () => {
    apiClient.get("/messages/unread-count")
      .then((r) => setUnreadMessages(r.data?.data?.unreadCount ?? 0))
      .catch(() => {});
  };

  useEffect(() => {
    loadUnreadMessages();
    const interval = setInterval(() => {
      if (document.hidden) return;
      loadUnreadMessages();
    }, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  const handleSignOut = () => {
    setProfileAnchor(null);
    logout();
    navigate("/login");
  };

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const loadNotifications = () => {
    apiClient.get("/dashboard/notifications").then((r) => setNotifications(r.data.data)).catch(() => {});
  };

  useEffect(() => {
    // The first fetch waits so it doesn't compete with the page's own data
    // requests on load - on the production instance it took 9.9 s doing so.
    const firstLoad = setTimeout(loadNotifications, FIRST_LOAD_DELAY_MS);
    const interval = setInterval(() => {
      if (document.hidden) return;
      loadNotifications();
    }, POLL_INTERVAL_MS);
    return () => {
      clearTimeout(firstLoad);
      clearInterval(interval);
    };
  }, []);

  const handleNotificationClick = (notification: NotificationDto) => {
    setBellAnchor(null);
    if (notification.id !== null) {
      apiClient.post(`/dashboard/notifications/${notification.id}/read`).catch(() => {});
      setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)));
    }
    const target = notificationTarget(notification);
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
            slotProps={{
              input: { "aria-label": mode === "dark" ? "Switch to light mode" : "Switch to dark mode" }
            }}
          />
        </Tooltip>
        <Tooltip title="Notifications">
          <IconButton
            onClick={(e) => setBellAnchor(e.currentTarget)}
            sx={{ color: "#fff" }}
            aria-label={`Notifications${unreadCount > 0 ? `, ${unreadCount} unread` : ""}`}
            aria-controls={bellAnchor ? "header-notifications-menu" : undefined}
            aria-haspopup="true"
            aria-expanded={Boolean(bellAnchor)}
          >
            <Badge badgeContent={unreadCount} color="error">
              <NotificationsIcon />
            </Badge>
          </IconButton>
        </Tooltip>
        <Menu
          id="header-notifications-menu"
          anchorEl={bellAnchor}
          open={Boolean(bellAnchor)}
          onClose={() => setBellAnchor(null)}
          slotProps={{ paper: { sx: { width: { xs: "calc(100vw - 24px)", sm: 360 }, maxWidth: 360, maxHeight: 420 } } }}
        >
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
              <ListItemText primary="No notifications" secondary="You are up to date - nothing needs your attention." />
            </MenuItem>
          )}
          {notifications.map((n, i) => {
            const target = notificationTarget(n);
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
                  slotProps={{
                    primary: { sx: { fontWeight: n.isRead ? 400 : 700, fontSize: 13 } },
                    secondary: { sx: { fontSize: 11 } }
                  }} />
              </MenuItem>
            );
          })}
        </Menu>

        <Tooltip title="Account">
          <ButtonBase
            onClick={(e) => setProfileAnchor(e.currentTarget)}
            aria-label={`Account menu for ${displayName}${unreadMessages > 0 ? `, ${unreadMessages} unread messages` : ""}`}
            aria-controls={profileAnchor ? "header-account-menu" : undefined}
            aria-haspopup="true"
            aria-expanded={Boolean(profileAnchor)}
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              pl: 0.5,
              pr: showIdentityText ? 1 : 0.5,
              py: 0.5,
              ml: 0.5,
              borderRadius: 2,
              color: "#fff",
              textAlign: "left",
              bgcolor: profileAnchor ? "rgba(255,255,255,0.14)" : "transparent",
              "&:hover": { bgcolor: "rgba(255,255,255,0.1)" },
              "&.Mui-focusVisible": { outline: "2px solid #fff", outlineOffset: 2 }
            }}
          >
            <Badge badgeContent={unreadMessages} color="error" overlap="circular">
              <Avatar sx={{ width: 32, height: 32, bgcolor: "#fff", color: "primary.main", fontWeight: 700, fontSize: 13 }}>
                {userInitials(fullName, username)}
              </Avatar>
            </Badge>
            {showIdentityText && (
              <Box sx={{ minWidth: 0, maxWidth: 220 }}>
                <Typography noWrap sx={{ fontSize: 13, fontWeight: 600, lineHeight: 1.25 }}>{displayName}</Typography>
                <Typography noWrap sx={{ fontSize: 11.5, lineHeight: 1.25, color: "rgba(255,255,255,0.82)" }}>
                  {username ? `${username} · ${displayTitle}` : displayTitle}
                </Typography>
              </Box>
            )}
            {showIdentityText && <ExpandMoreIcon sx={{ fontSize: 18, color: "rgba(255,255,255,0.8)" }} />}
          </ButtonBase>
        </Tooltip>
        <Menu
          id="header-account-menu"
          anchorEl={profileAnchor}
          open={Boolean(profileAnchor)}
          onClose={() => setProfileAnchor(null)}
          anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
          transformOrigin={{ vertical: "top", horizontal: "right" }}
          slotProps={{ paper: { sx: { minWidth: 260, mt: 0.5 } } }}
        >
          <Box sx={{ px: 2, py: 1.25 }}>
            <Typography sx={{ fontWeight: 600, fontSize: 14 }}>{displayName}</Typography>
            <Typography sx={{ fontSize: 12.5, color: "text.secondary" }}>{displayTitle}</Typography>
            <Box component="dl" sx={{ m: 0, mt: 0.75, display: "grid", gridTemplateColumns: "auto 1fr", columnGap: 1, fontSize: 12, color: "text.secondary", "& dd": { m: 0, color: "text.primary" } }}>
              {username && (<><dt>User ID</dt><dd>{username}</dd></>)}
              {role && (<><dt>Role</dt><dd>{ROLE_LABELS[role] ?? role}</dd></>)}
            </Box>
          </Box>
          <Divider />
          <MenuItem component={Link} to="/profile" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon><PersonOutlineIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary="My Profile" />
          </MenuItem>
          <MenuItem component={Link} to="/messages" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon>
              <Badge badgeContent={unreadMessages} color="error">
                <MailOutlineIcon fontSize="small" />
              </Badge>
            </ListItemIcon>
            <ListItemText primary={unreadMessages > 0 ? `Messages (${unreadMessages} unread)` : "Messages"} />
          </MenuItem>
          <MenuItem component={Link} to="/discussions" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon><ForumOutlinedIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary="Discussions" />
          </MenuItem>
          <MenuItem component={Link} to="/change-password" onClick={() => setProfileAnchor(null)}>
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
