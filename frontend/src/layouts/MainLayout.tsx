import { Outlet, useNavigate, useLocation, Navigate } from "react-router-dom";
import { Box, Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button } from "@mui/material";
import { Header } from "../components/Header";
import { Sidebar } from "../components/Sidebar";
import { useAuth } from "../contexts/AuthContext";
import { useIdleTimeout } from "../hooks/useIdleTimeout";

const CHANGE_PASSWORD_PATH = "/change-password";

// Topbar + subnav stacked at the top, content below - matches the
// provided design's page structure for every route in the app.
export function MainLayout() {
  const { logout, mustChangePassword } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

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
    <Box sx={{ minHeight: "100vh", bgcolor: "background.default" }}>
      <Header />
      <Sidebar />
      <Box component="main" sx={{ px: 3, py: 2.75, maxWidth: 1400, mx: "auto" }}>
        <Outlet />
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
