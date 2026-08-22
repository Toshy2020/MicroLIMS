import { useState } from "react";
import { Outlet, useNavigate, useLocation, Navigate } from "react-router-dom";
import { Box, Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button, useMediaQuery, useTheme } from "@mui/material";
import { Header } from "../components/Header";
import { Sidebar } from "../components/Sidebar";
import { useAuth } from "../contexts/AuthContext";
import { useIdleTimeout } from "../hooks/useIdleTimeout";

const CHANGE_PASSWORD_PATH = "/change-password";

export function MainLayout() {
  const { logout, mustChangePassword } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

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
        height: "100vh",
        maxHeight: "100vh",
        bgcolor: "background.default",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden"
      }}
    >
      <Header onToggleSidebar={handleToggleSidebar} sidebarCollapsed={collapsed} />

      <Box
        component="div"
        sx={{
          display: "flex",
          flex: 1,
          height: "calc(100vh - 56px)",
          overflow: "hidden",
          position: "relative"
        }}
      >
        <Sidebar
          mobileOpen={mobileOpen}
          onMobileClose={() => setMobileOpen(false)}
          collapsed={collapsed}
          onToggleCollapse={handleToggleCollapse}
        />

        <Box
          component="main"
          sx={{
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
            <Outlet />
          </Box>
        </Box>
      </Box>

      {/* GMP session-timeout control - not dismissable via escape/backdrop, no "stay logged out" bypass. */}
      <Dialog open={showWarning} disableEscapeKeyDown onClose={() => {}}>
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
