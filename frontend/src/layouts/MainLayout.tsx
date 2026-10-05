import { useMemo, useState } from "react";
import { Outlet, useNavigate, useLocation, Navigate } from "react-router-dom";
import { Box, Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button, useMediaQuery, useTheme } from "@mui/material";
import { Header } from "../components/Header";
import { Sidebar } from "../components/Sidebar";
import { AppBreadcrumbs } from "../components/AppBreadcrumbs";
import { AppErrorBoundary } from "../components/AppErrorBoundary";
import { useAuth } from "../contexts/AuthContext";
import { useIdleTimeout } from "../hooks/useIdleTimeout";
import { useMyLabs } from "../hooks/useMyLabs";
import { useMenuGroups } from "../hooks/useMenuGroups";
import { accountPageTrail, findNavTrail } from "../routes/navigation";

const MAIN_CONTENT_ID = "main-content";

const CHANGE_PASSWORD_PATH = "/change-password";

export function MainLayout() {
  const { logout, mustChangePassword } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  // One menu, one lab lookup and one "where am I" answer for the sidebar,
  // the breadcrumb and the tab title.
  const { codes: labCodes } = useMyLabs();
  const groups = useMenuGroups();
  const activeTrail = useMemo(
    () => findNavTrail(groups, location.pathname, location.search) ?? accountPageTrail(location.pathname),
    [groups, location.pathname, location.search]
  );

  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem("microlims_sidebar_collapsed") === "true");

  const handleToggleCollapse = () => {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem("microlims_sidebar_collapsed", String(next));
      return next;
    });
  };

  // On desktop the hamburger toggles the persistent icon-only rail; on
  // mobile there's no rail mode (see Sidebar's effectiveCollapsed), so it
  // opens/closes the temporary drawer instead.
  const handleToggleSidebar = () => {
    if (isMobile) {
      setMobileOpen((prev) => !prev);
    } else {
      handleToggleCollapse();
    }
  };

  const handleIdleTimeout = () => {
    logout();
    navigate("/login");
  };

  const { showWarning, secondsRemaining, stayLoggedIn } = useIdleTimeout(handleIdleTimeout);

  // A seeded/admin-set password can't be kept forever - block navigation
  // anywhere else until the forced change is done.
  if (mustChangePassword && location.pathname !== CHANGE_PASSWORD_PATH) {
    return <Navigate to={CHANGE_PASSWORD_PATH} replace />;
  }

  return (
    <Box
      sx={{
        // dvh where supported: on mobile browsers 100vh includes the area
        // under the address bar, which cut off the bottom of every page.
        height: "100vh",
        maxHeight: "100vh",
        "@supports (height: 100dvh)": { height: "100dvh", maxHeight: "100dvh" },
        bgcolor: "background.default",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden"
      }}
    >
      {/* Keyboard users land here first and can jump past the navigation. */}
      <Box
        component="a"
        href={`#${MAIN_CONTENT_ID}`}
        className="no-print"
        sx={{
          position: "absolute",
          left: 8,
          top: -48,
          zIndex: 2000,
          px: 2,
          py: 1,
          borderRadius: 1,
          bgcolor: "background.paper",
          color: "primary.main",
          fontWeight: 600,
          fontSize: 14,
          boxShadow: 3,
          "&:focus": { top: 8 }
        }}
      >
        Skip to main content
      </Box>

      <Header onToggleSidebar={handleToggleSidebar} sidebarCollapsed={collapsed} labCodes={labCodes} />

      <Box
        component="div"
        sx={{
          display: "flex",
          flex: 1,
          minHeight: 0,
          overflow: "hidden",
          position: "relative"
        }}
      >
        <Sidebar
          mobileOpen={mobileOpen}
          onMobileClose={() => setMobileOpen(false)}
          collapsed={collapsed}
          onToggleCollapse={handleToggleCollapse}
          groups={groups}
          activeTrail={activeTrail}
        />

        <Box
          component="main"
          id={MAIN_CONTENT_ID}
          tabIndex={-1}
          sx={{
            outline: "none",
            flexGrow: 1,
            height: "100%",
            overflowY: "auto",
            overflowX: "hidden",
            p: { xs: 2, sm: 3 },
            width: "100%",
            minWidth: 0,
            bgcolor: "background.default"
          }}
        >
          <Box sx={{ maxWidth: 1600, mx: "auto" }}>
            <AppBreadcrumbs trail={activeTrail} groups={groups} />
            <AppErrorBoundary inline key={location.pathname}>
              <Outlet />
            </AppErrorBoundary>
          </Box>
        </Box>
      </Box>

      {/* GMP session-timeout control - not dismissable via escape/backdrop, no "stay logged out" bypass.
          onClose ignores every close reason (escapeKeyDown, backdropClick), which is what keeps it open;
          MUI v9 removed disableEscapeKeyDown in favour of exactly this. */}
      <Dialog open={showWarning} onClose={() => {}}>
        <DialogTitle>Session Timeout Warning</DialogTitle>
        <DialogContent>
          <DialogContentText>
            You will be signed out due to inactivity in {secondsRemaining} second{secondsRemaining === 1 ? "" : "s"}.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={stayLoggedIn} autoFocus>Stay signed in</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
