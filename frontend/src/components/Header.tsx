import { useCallback, useEffect, useState, type ReactNode } from "react";
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
import SearchIcon from "@mui/icons-material/Search";
import { apiClient } from "../services/apiClient";
import { useThemeMode } from "../theme/ThemeModeContext";
import { useBenchMode } from "../theme/BenchModeContext";
import { useAuth } from "../contexts/AuthContext";
import { formatDateTimeWithTimeZone } from "../utils/formatDate";
import { useLive } from "../contexts/LiveContext";
import { useLiveRefresh } from "../hooks/useLiveRefresh";
import { formatRoleFallback, formatRoleName, userInitials } from "../utils/userDisplay";
import { GlobalSearchDialog } from "./search/GlobalSearchDialog";
import { useGlobalSearchShortcut } from "../hooks/useGlobalSearchShortcut";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CloudOffOutlinedIcon from "@mui/icons-material/CloudOffOutlined";
import { visuallyHidden } from "@mui/utils";
import { LanguageMenuItems } from "./LanguageMenuItems";
import { Ltr } from "./Ltr";
import { useTranslation } from "../i18n";
import { HelpMenu } from "../modules/help/HelpMenu";
import { ConfirmationDialog } from "./ConfirmationDialog";
import SwitchAccountIcon from "@mui/icons-material/SwitchAccount";
import { NOTIFICATION_ROUTES, resolveNotificationTarget } from "../modules/notifications/notificationRoutes";

export { NOTIFICATION_ROUTES };

const BRAND_PREFIX = "Micro";
const BRAND_SUFFIX = "LIMS";
const SEARCH_SHORTCUT = "Ctrl+K";
const NOTIFICATION_FILTERS = ["all", "unread"] as const;

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

const BELL_MAX_ITEMS = 10;

const POLL_INTERVAL_MS = 60_000;
const FIRST_LOAD_DELAY_MS = 5_000;

interface HeaderProps {
  onToggleSidebar?: () => void;
  sidebarCollapsed?: boolean;
  // Lab membership, for the job-title fallback ("Microbiology Analyst").
  labCodes: string[];
  // Unsaved on-tablet drafts; the bench drafts feature supplies the real count.
  unsavedDraftCount?: number;
  // The unsaved-entries menu (DraftsMenu), rendered next to Switch user.
  draftsMenu?: ReactNode;
}

export function Header({ onToggleSidebar, sidebarCollapsed, labCodes, unsavedDraftCount = 0, draftsMenu }: HeaderProps) {
  const { t } = useTranslation("navigation");
  const { mode, toggleMode } = useThemeMode();
  const { benchMode, setBenchMode } = useBenchMode();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const showIdentityText = useMediaQuery(theme.breakpoints.up("md"));
  // Phones: shrink the two switches so the bell and account button stay inside the bar.
  const isCompactHeader = useMediaQuery(theme.breakpoints.down("sm"));
  const navigate = useNavigate();
  const { username, fullName, jobTitle, role, roleName, logout } = useAuth();
  const { status: liveStatus } = useLive();

  const displayName = fullName ?? username ?? t("header.defaultUser");
  const displayTitle = jobTitle?.trim() || formatRoleFallback(role, labCodes, roleName, t);

  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [bellAnchor, setBellAnchor] = useState<HTMLElement | null>(null);
  const [profileAnchor, setProfileAnchor] = useState<HTMLElement | null>(null);
  const [unreadMessages, setUnreadMessages] = useState<number>(0);
  const [searchOpen, setSearchOpen] = useState<boolean>(false);

  useGlobalSearchShortcut(() => setSearchOpen((prev) => !prev));

  const loadUnreadMessages = useCallback(() => {
    apiClient.get("/messages/unread-count")
      .then((r) => setUnreadMessages(r.data?.data?.unreadCount ?? 0))
      // Badge only: keep the last known count and let the next refresh retry.
      .catch(() => {});
  }, []);

  useEffect(() => {
    loadUnreadMessages();
  }, [loadUnreadMessages]);

  useLiveRefresh({
    kind: "messages",
    refetch: loadUnreadMessages,
    fallbackMs: POLL_INTERVAL_MS
  });

  const handleSignOut = () => {
    setProfileAnchor(null);
    logout();
    navigate("/login");
  };

  const [switchUserConfirmOpen, setSwitchUserConfirmOpen] = useState(false);
  const handleSwitchUser = () => {
    if (unsavedDraftCount > 0) {
      setSwitchUserConfirmOpen(true);
      return;
    }
    handleSignOut();
  };

  const unreadCount = notifications.filter((n) => !n.isRead).length;
  const [notificationFilter, setNotificationFilter] = useState<"all" | "unread">("all");
  const visibleNotifications = (
    notificationFilter === "unread" ? notifications.filter((n) => !n.isRead) : notifications
  ).slice(0, BELL_MAX_ITEMS);

  // Set while the latest poll failed and nothing has loaded since. Without it
  // the bell said "No notifications - you are up to date" during an outage.
  const [notificationsFailed, setNotificationsFailed] = useState(false);

  const loadNotifications = useCallback(() => {
    apiClient.get("/dashboard/notifications")
      .then((r) => {
        setNotifications(r.data.data);
        setNotificationsFailed(false);
      })
      .catch(() => setNotificationsFailed(true));
  }, []);

  useEffect(() => {
    // The first fetch waits so it doesn't compete with the page's own data
    // requests on load - on the production instance it took 9.9 s doing so.
    const firstLoad = setTimeout(loadNotifications, FIRST_LOAD_DELAY_MS);
    return () => {
      clearTimeout(firstLoad);
    };
  }, [loadNotifications]);

  useLiveRefresh({
    kind: "notifications",
    refetch: loadNotifications,
    fallbackMs: POLL_INTERVAL_MS
  });

  const handleNotificationClick = (notification: NotificationDto) => {
    setBellAnchor(null);
    if (notification.id !== null) {
      // Fire-and-forget: shown as read at once; if the call failed, the next
      // poll brings the true state back.
      apiClient.post(`/dashboard/notifications/${notification.id}/read`).catch(() => {});
      setNotifications((prev) => prev.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)));
    }
    const target = resolveNotificationTarget(notification);
    if (target) navigate(target);
  };

  const handleMarkAllRead = () => {
    if (unreadCount === 0) return;
    // Fire-and-forget, as for a single notification above.
    apiClient.post("/dashboard/notifications/read-all").catch(() => {});
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  };

  return (
    <Box
      component="header"
      className="no-print"
      sx={{
        background: theme.custom.chrome.topbarBg,
        color: theme.custom.chrome.topbarText,
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        px: { xs: 1.5, sm: 3 },
        height: 56,
        minHeight: 56,
        maxHeight: 56,
        flexShrink: 0,
        borderBottom: `1px solid ${theme.custom.chrome.topbarBorder}`,
        position: "relative",
        zIndex: 1100
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {onToggleSidebar && (
          <Tooltip title={isMobile ? t("header.toggleMenu") : (sidebarCollapsed ? t("header.expandSidebar") : t("header.collapseSidebar"))}>
            <IconButton
              onClick={onToggleSidebar}
              sx={{ color: "inherit", p: 0.75, mr: 0.5 }}
              aria-label={t("header.toggleMenu")}
            >
              <MenuIcon />
            </IconButton>
          </Tooltip>
        )}
        <Typography dir="ltr" sx={{ fontSize: { xs: 19, sm: 22 }, fontWeight: 700, letterSpacing: 0.5, userSelect: "none" }}>
          {BRAND_PREFIX}<Box component="span" sx={{ fontWeight: 300, color: theme.custom.chrome.brandAccent }}>{BRAND_SUFFIX}</Box>
        </Typography>
      </Box>

      <ButtonBase
        onClick={() => setSearchOpen(true)}
        data-tour="global-search"
        sx={{
          display: { xs: "none", sm: "flex" },
          alignItems: "center",
          gap: 1.5,
          px: 2,
          py: 0.6,
          bgcolor: theme.custom.chrome.topbarControlBg,
          borderRadius: 2,
          color: theme.custom.chrome.topbarMutedText,
          width: { sm: 180, md: 320, lg: 360 },
          justifyContent: "space-between",
          cursor: "pointer",
          "&:hover": { bgcolor: theme.custom.chrome.topbarControlHoverBg }
        }}
        aria-label={t("header.searchShortcutAria")}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <SearchIcon sx={{ fontSize: 18, opacity: 0.8 }} />
          <Typography sx={{ fontSize: 13, opacity: 0.85 }}>{t("header.searchPlaceholder")}</Typography>
        </Box>
        <Box sx={{ fontSize: 11, border: `1px solid ${theme.custom.chrome.topbarBorder}`, bgcolor: theme.custom.chrome.topbarBg, px: 0.8, py: 0.2, borderRadius: 1 }}>
          <Ltr>{SEARCH_SHORTCUT}</Ltr>
        </Box>
      </ButtonBase>

      <Box sx={{ display: "flex", alignItems: "center", gap: { xs: 0.25, sm: 0.75, md: 1.5 }, minWidth: 0, flexShrink: 0 }}>
        <Tooltip title={t("header.searchButton")}>
          <IconButton
            onClick={() => setSearchOpen(true)}
            data-tour="global-search"
            sx={{ color: "inherit", display: { sm: "none" } }}
            aria-label={t("header.searchButton")}
          >
            <SearchIcon />
          </IconButton>
        </Tooltip>
        <Tooltip title={t("header.benchModeTooltip")}>
          <Switch
            size={isCompactHeader ? "small" : "medium"}
            checked={benchMode}
            onChange={(_, checked) => setBenchMode(checked)}
            sx={{
              "& .MuiSwitch-track": { backgroundColor: theme.custom.chrome.topbarSwitchTrack, opacity: 1 },
              "& .MuiSwitch-thumb": { backgroundColor: "common.white", boxShadow: "0 1px 2px rgba(0,0,0,0.3)" },
              "& .Mui-checked+.MuiSwitch-track": { backgroundColor: `${theme.custom.chrome.topbarSwitchTrack} !important`, opacity: 1 }
            }}
            slotProps={{ input: { "aria-label": t("header.benchModeLabel") } }}
          />
        </Tooltip>
        <Tooltip title={mode === "dark" ? t("header.switchToLight") : t("header.switchToDark")}>
          <Switch
            size={isCompactHeader ? "small" : "medium"}
            checked={mode === "dark"}
            onChange={toggleMode}
            icon={<LightModeIcon sx={{ fontSize: 15, color: "#f2b705", p: "1.5px" }} />}
            checkedIcon={<DarkModeIcon sx={{ fontSize: 15, color: "#E7EBF0", p: "1.5px" }} />}
            sx={{
              "& .MuiSwitch-track": { backgroundColor: theme.custom.chrome.topbarSwitchTrack, opacity: 1 },
              "& .MuiSwitch-thumb": { backgroundColor: "common.white", boxShadow: "0 1px 2px rgba(0,0,0,0.3)" },
              "& .Mui-checked+.MuiSwitch-track": { backgroundColor: `${theme.custom.chrome.topbarSwitchTrack} !important`, opacity: 1 }
            }}
            slotProps={{
              input: { "aria-label": mode === "dark" ? t("header.switchToLight") : t("header.switchToDark") }
            }}
          />
        </Tooltip>
        {benchMode && (
          <Button
            color="inherit"
            startIcon={<SwitchAccountIcon />}
            onClick={handleSwitchUser}
            sx={{ whiteSpace: "nowrap" }}
          >
            {t("header.switchUser")}
          </Button>
        )}
        {draftsMenu}
        {liveStatus === "offline" && (
          <Tooltip title={t("header.liveUpdatesPausedTooltip")}>
            <Box
              component="span"
              role="status"
              aria-label={t("header.liveUpdatesPaused")}
              aria-live="polite"
              sx={{
                display: "inline-flex",
                alignItems: "center",
                gap: 0.5,
                px: 1,
                py: 0.25,
                borderRadius: 5,
                fontSize: 12,
                fontWeight: 600,
                lineHeight: 1.4,
                whiteSpace: "nowrap",
                bgcolor: theme.custom.status.inconclusive.bg,
                color: theme.custom.status.inconclusive.text,
                border: `1px solid ${theme.custom.status.inconclusive.border}`
              }}
            >
              <CloudOffOutlinedIcon aria-hidden="true" sx={{ fontSize: 14 }} />
              {t("header.liveUpdatesPaused")}
            </Box>
          </Tooltip>
        )}
        <HelpMenu />
        <Tooltip title={t("header.notifications")}>
          <IconButton
            onClick={(e) => setBellAnchor(e.currentTarget)}
            data-tour="notifications"
            sx={{ color: "inherit" }}
            aria-label={unreadCount > 0 ? t("header.notificationsUnreadAria", { count: unreadCount }) : t("header.notifications")}
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
          anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
          transformOrigin={{ vertical: "top", horizontal: "right" }}
          slotProps={{
            paper: {
              sx: {
                width: { xs: "calc(100vw - 24px)", sm: 380 }, maxWidth: 380, maxHeight: 460, mt: 0.75,
                boxShadow: "0 10px 25px rgba(0,0,0,0.12)", border: "1px solid", borderColor: "divider"
              }
            }
          }}
        >
          {/* Header and filter sit outside the menu's item navigation: keys
              typed here are theirs, not the menu's type-ahead. */}
          <Box onKeyDown={(e) => e.stopPropagation()} sx={{ px: 2, pt: 1, pb: 0.75 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 14, fontWeight: 700 }}>{t("header.notifications")}</Typography>
              <Button
                size="small"
                onClick={handleMarkAllRead}
                disabled={unreadCount === 0}
                sx={{ fontSize: 12, textTransform: "none", minWidth: 0 }}
              >
                {t("header.markAllRead")}
              </Button>
            </Box>
            <Box role="group" aria-label={t("header.showNotificationsAria")} sx={{ display: "flex", gap: 0.5, mt: 0.5 }}>
              {NOTIFICATION_FILTERS.map((f) => (
                <Button
                  key={f}
                  size="small"
                  aria-pressed={notificationFilter === f}
                  onClick={() => setNotificationFilter(f)}
                  sx={{
                    fontSize: 12, textTransform: "none", minWidth: 0, px: 1.25, py: 0.25, borderRadius: 5,
                    color: notificationFilter === f ? "primary.main" : "text.secondary",
                    bgcolor: notificationFilter === f ? "action.selected" : "transparent"
                  }}
                >
                  {f === "all"
                    ? t("header.filterAll")
                    : (unreadCount > 0 ? t("header.filterUnreadCount", { count: unreadCount }) : t("header.filterUnread"))}
                </Button>
              ))}
            </Box>
          </Box>
          <Divider />
          {notificationsFailed && (
            <MenuItem disabled sx={{ whiteSpace: "normal", "&.Mui-disabled": { opacity: 1 } }}>
              <ListItemText
                primary={t("header.notificationsLoadFailed")}
                secondary={notifications.length > 0 ? t("header.notificationsOutOfDate") : t("header.notificationsRetrying")}
                slotProps={{ primary: { sx: { color: "error.main", fontSize: 13, fontWeight: 600 } } }}
              />
            </MenuItem>
          )}
          {visibleNotifications.length === 0 && !notificationsFailed && (
            <MenuItem disabled sx={{ whiteSpace: "normal" }}>
              <ListItemText
                primary={notificationFilter === "unread" && notifications.length > 0 ? t("header.noUnreadNotifications") : t("header.noNotifications")}
                secondary={t("header.notificationsUpToDate")}
              />
            </MenuItem>
          )}
          {visibleNotifications.map((n, i) => {
            const target = resolveNotificationTarget(n);
            const warning = n.severity === "warning" || n.severity === "error";
            return (
              <MenuItem
                key={n.id ?? i}
                {...(target ? { component: Link, to: target } : {})}
                onClick={() => handleNotificationClick(n)}
                sx={{ whiteSpace: "normal", alignItems: "flex-start", gap: 1.25, py: 1 }}
              >
                {warning
                  ? <WarningAmberOutlinedIcon fontSize="small" sx={{ color: "warning.main", mt: 0.25 }} titleAccess={t("header.warning")} />
                  : <InfoOutlinedIcon fontSize="small" sx={{ color: "info.main", mt: 0.25 }} titleAccess={t("header.information")} />}
                <ListItemText
                  sx={{ m: 0 }}
                  primary={
                    <>
                      <Box component="span" sx={visuallyHidden}>
                        {warning ? t("header.warningPrefix") : t("header.informationPrefix")}
                      </Box>
                      {n.message}
                    </>
                  }
                  secondary={formatDateTimeWithTimeZone(n.timestamp)}
                  slotProps={{
                    primary: { sx: { fontWeight: n.isRead ? 400 : 700, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }} />
                {!n.isRead && (
                  <Box
                    component="span"
                    title={t("header.unread")}
                    sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: "primary.main", flexShrink: 0, mt: 0.75 }}
                  >
                    <Box component="span" sx={visuallyHidden}>{t("header.unread")}</Box>
                  </Box>
                )}
              </MenuItem>
            );
          })}
          <Divider />
          {/* A menu item, so arrow keys in the bell menu reach it like every other entry. */}
          <MenuItem
            component={Link}
            to="/notifications"
            onClick={() => setBellAnchor(null)}
            sx={{ justifyContent: "center", fontWeight: 600, color: "primary.main" }}
          >
            {t("header.seeAll")}
          </MenuItem>
        </Menu>

        <Tooltip title={t("header.account")}>
          <ButtonBase
            onClick={(e) => setProfileAnchor(e.currentTarget)}
            aria-label={unreadMessages > 0
              ? t("header.accountMenuUnreadAria", { name: displayName, count: unreadMessages })
              : t("header.accountMenuAria", { name: displayName })}
            aria-controls={profileAnchor ? "header-account-menu" : undefined}
            aria-haspopup="true"
            aria-expanded={Boolean(profileAnchor)}
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1,
              minWidth: 0,
              pl: 0.5,
              pr: showIdentityText ? 1 : 0.5,
              py: 0.5,
              ml: 0.5,
              borderRadius: 2,
              color: "inherit",
              textAlign: "left",
              bgcolor: profileAnchor ? theme.custom.chrome.topbarControlHoverBg : "transparent",
              "&:hover": { bgcolor: theme.custom.chrome.topbarControlBg },
              "&.Mui-focusVisible": { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 2 }
            }}
          >
            <Badge badgeContent={unreadMessages} color="error" overlap="circular">
              <Avatar sx={{ width: 32, height: 32, bgcolor: "primary.main", color: "primary.contrastText", fontWeight: 700, fontSize: 13 }}>
                {userInitials(fullName, username)}
              </Avatar>
            </Badge>
            {showIdentityText && (
              <Box sx={{ minWidth: 0, maxWidth: 220 }}>
                <Typography noWrap sx={{ fontSize: 13, fontWeight: 600, lineHeight: 1.25 }}>{displayName}</Typography>
                <Typography noWrap sx={{ fontSize: 12, lineHeight: 1.25, color: theme.custom.chrome.topbarMutedText }}>
                  {username ? <><Ltr>{username}</Ltr> · {displayTitle}</> : displayTitle}
                </Typography>
              </Box>
            )}
            {showIdentityText && <ExpandMoreIcon sx={{ fontSize: 18, color: theme.custom.chrome.topbarMutedText }} />}
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
              {username && (<><dt>{t("header.userId")}</dt><dd><Ltr>{username}</Ltr></dd></>)}
              {(roleName || role) && (<><dt>{t("header.role")}</dt><dd>{formatRoleName(role, roleName, t)}</dd></>)}
            </Box>
          </Box>
          <Divider />
          <MenuItem component={Link} to="/profile" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon><PersonOutlineIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary={t("item.myProfile")} />
          </MenuItem>
          <MenuItem component={Link} to="/messages" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon>
              <Badge badgeContent={unreadMessages} color="error">
                <MailOutlineIcon fontSize="small" />
              </Badge>
            </ListItemIcon>
            <ListItemText primary={unreadMessages > 0 ? t("header.messagesUnread", { count: unreadMessages }) : t("item.messages")} />
          </MenuItem>
          <MenuItem component={Link} to="/discussions" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon><ForumOutlinedIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary={t("item.discussions")} />
          </MenuItem>
          <MenuItem component={Link} to="/change-password" onClick={() => setProfileAnchor(null)}>
            <ListItemIcon><LockResetIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary={t("item.changePassword")} />
          </MenuItem>
          <Divider />
          <LanguageMenuItems onChosen={() => setProfileAnchor(null)} />
          <Divider />
          <MenuItem onClick={handleSignOut}>
            <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
            <ListItemText primary={t("header.signOut")} />
          </MenuItem>
        </Menu>
      </Box>
      <GlobalSearchDialog open={searchOpen} onClose={() => setSearchOpen(false)} />
      <ConfirmationDialog
        open={switchUserConfirmOpen}
        title={t("header.switchUser")}
        destructive
        confirmText={t("header.switchUser")}
        message={t("header.switchUserUnsaved", { count: unsavedDraftCount })}
        onCancel={() => setSwitchUserConfirmOpen(false)}
        onConfirm={() => {
          setSwitchUserConfirmOpen(false);
          handleSignOut();
        }}
      />
    </Box>
  );
}
