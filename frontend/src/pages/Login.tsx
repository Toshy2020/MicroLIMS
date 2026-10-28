import { useEffect, useState } from "react";
import {
  Box, TextField, Button, Typography, Alert, Link, Stack, useTheme,
  IconButton, InputAdornment, CircularProgress
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { useNavigate, Link as RouterLink } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { LoginBrandArt } from "./LoginBrandArt";

export function LoginPage() {
  const theme = useTheme();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { login } = useAuth();
  const navigate = useNavigate();

  const [forgotMode, setForgotMode] = useState(false);
  const [resetUsername, setResetUsername] = useState("");
  const [resetMessage, setResetMessage] = useState<string | null>(null);
  const [resetSubmitting, setResetSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const { token, refreshToken, role, roleName, permissions, mustChangePassword } = await authenticationService.login(username, password);
      localStorage.setItem("microlims_token", token);
      const me = await authenticationService.me();
      login({ token, refreshToken, username, role, roleName: roleName ?? me.roleName ?? null, permissions, fullName: me.fullName, userId: me.userId, mustChangePassword });
      navigate("/dashboard");
    } catch (err: any) {
      if (err?.response?.status === 401) {
        setError("Invalid username or password.");
      } else if (err?.response?.status >= 500) {
        setError("Laboratory authentication service error. Please try again later.");
      } else if (!err?.response) {
        setError("Unable to reach the laboratory server. Check network connection.");
      } else {
        setError(err?.response?.data?.message || "Login failed. Please check credentials.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleForgotSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setResetSubmitting(true);
    try {
      await authenticationService.requestPasswordReset(resetUsername);
    } catch {
      // Swallow
    } finally {
      setResetSubmitting(false);
      setResetMessage("If that account exists, a reset link has been generated.");
    }
  };

  // The tab otherwise keeps the title of the page the user signed out from.
  useEffect(() => {
    document.title = "Sign in · MicroLIMS";
  }, []);

  const chrome = theme.custom.chrome;

  return (
    // Split screen: a brand panel (40%) saying what the system is, and the
    // sign-in form (60%). On a phone the panel shrinks to a short band above
    // the form and drops its drawings.
    <Box
      component="main"
      sx={{
        minHeight: "calc(100vh - var(--system-banner-height, 0px))",
        display: "grid",
        gridTemplateColumns: { xs: "1fr", md: "40% 60%" },
        gridTemplateRows: { xs: "auto 1fr", md: "1fr" },
        bgcolor: "background.paper"
      }}
    >
      <Box
        component="section"
        aria-label="About MicroLIMS"
        sx={{
          position: "relative",
          overflow: "hidden",
          background: chrome.brandBandBg,
          color: "common.white",
          display: "flex",
          flexDirection: "column",
          px: { xs: 2.5, md: 7 },
          pt: { xs: 2.5, md: 6 },
          pb: { xs: 2.25, md: 0 }
        }}
      >
        <Box
          aria-hidden="true"
          sx={{
            position: "absolute", width: 520, height: 520, right: -220, top: -160, borderRadius: "50%",
            background: `radial-gradient(circle, ${chrome.brandBandGlow} 0%, transparent 70%)`
          }}
        />
        <Typography sx={{ position: "relative", fontSize: { xs: 26, md: 30 }, fontWeight: 700, letterSpacing: 0.5, lineHeight: 1.2 }}>
          Micro<Box component="span" sx={{ fontWeight: 300, color: chrome.brandBandAccent }}>LIMS</Box>
        </Typography>
        <Box sx={{ position: "relative", my: { xs: 0, md: "auto" }, mt: { xs: 1.5 }, pt: { md: 4 } }}>
          <Typography component="h2" sx={{ fontSize: { xs: 18, md: 30 }, fontWeight: 700, lineHeight: 1.2, maxWidth: 420 }}>
            Pharmaceutical Quality Control
          </Typography>
          <Typography sx={{ display: { xs: "none", md: "block" }, mt: 1, fontSize: 16, color: chrome.brandBandMuted }}>
            Laboratory Information Management System
          </Typography>
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, mt: { xs: 1.5, md: 2.75 } }}>
            {["FDA 21 CFR Part 11", "EU GMP Annex 11"].map((label) => (
              <Box
                key={label}
                component="span"
                sx={{
                  fontSize: 12, fontWeight: 600, px: 1.25, py: 0.5, borderRadius: 5,
                  bgcolor: chrome.brandBandChipBg, border: `1px solid ${chrome.brandBandChipBorder}`
                }}
              >
                {label}
              </Box>
            ))}
          </Box>
        </Box>
        {/* Room for the drawings, so the headline can never sit on them. The
            art keeps the panel's aspect: 290/546 of the panel's 40vw width. */}
        <Box sx={{ display: { xs: "none", md: "block" }, flexShrink: 0, height: "21.3vw", position: "relative" }}>
          <Typography sx={{ position: "absolute", left: 0, bottom: 32, fontSize: 12, color: chrome.brandBandMuted }}>
            v0.1
          </Typography>
        </Box>
        <LoginBrandArt
          sx={{
            display: { xs: "none", md: "block" },
            position: "absolute", left: 0, right: 0, bottom: 0, width: "100%", height: "auto",
            color: chrome.brandBandArt, pointerEvents: "none"
          }}
        />
      </Box>

      <Box sx={{ display: "flex", alignItems: { xs: "flex-start", md: "center" }, justifyContent: "center", px: { xs: 2.5, md: 3 }, py: { xs: 3.5, md: 5 } }}>
        <Box sx={{ width: "100%", maxWidth: 400 }}>
          {forgotMode ? (
            <Box component="form" onSubmit={handleForgotSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Box>
                <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>Need help signing in?</Typography>
                <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>
                  Enter your username and we'll generate a password reset request.
                </Typography>
              </Box>
              {resetMessage ? (
                <Alert severity="info">{resetMessage}</Alert>
              ) : (
                <>
                  <TextField label="Username" value={resetUsername} onChange={(e) => setResetUsername(e.target.value)} autoFocus required />
                  <Button type="submit" variant="contained" size="large" disabled={resetSubmitting}>
                    {resetSubmitting ? "Submitting..." : "Send Reset Link"}
                  </Button>
                </>
              )}
              <Stack spacing={1} sx={{ textAlign: "center" }}>
                <Link
                  component="button"
                  type="button"
                  underline="hover"
                  sx={{ fontSize: 14 }}
                  onClick={() => { setForgotMode(false); setResetMessage(null); setResetUsername(""); }}
                >
                  Back to sign in
                </Link>
                <Link component={RouterLink} to="/admin-recovery" underline="hover" sx={{ fontSize: 14 }}>
                  Have a recovery code from your administrator?
                </Link>
              </Stack>
            </Box>
          ) : (
            <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Box sx={{ mb: 1 }}>
                <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>Sign in</Typography>
                <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>Use your laboratory account.</Typography>
              </Box>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField
                label="Username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoComplete="username"
                autoFocus
                required
              />
              <TextField
                label="Password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
                slotProps={{
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          aria-label={showPassword ? "Hide password" : "Show password"}
                          onClick={() => setShowPassword((prev) => !prev)}
                          edge="end"
                          size="small"
                        >
                          {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                        </IconButton>
                      </InputAdornment>
                    )
                  }
                }}
              />
              <Button
                type="submit"
                variant="contained"
                size="large"
                disabled={isSubmitting}
                startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
                sx={{ mt: 0.5 }}
              >
                {isSubmitting ? "Signing in…" : "Sign In"}
              </Button>
              {/* One way into recovery: the help screen offers both a reset
                  request and the administrator recovery-code route. */}
              <Box sx={{ textAlign: "center" }}>
                <Link component="button" type="button" underline="hover" sx={{ fontSize: 14 }} onClick={() => setForgotMode(true)}>
                  Need help signing in?
                </Link>
              </Box>
            </Box>
          )}
          {/* Every attempt, successful or not, is written to LoginHistory
              (AuthenticationService.RecordLoginAsync). */}
          <Typography
            sx={{ mt: 4.5, pt: 2, borderTop: 1, borderColor: "divider", textAlign: "center", fontSize: 12, color: "text.secondary" }}
          >
            Authorised users only. Every sign-in attempt is recorded.
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
