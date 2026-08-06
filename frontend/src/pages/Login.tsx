import { useState } from "react";
import { Box, TextField, Button, Typography, Alert, Link } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { brandColors } from "../theme";

export function LoginPage() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const { login } = useAuth();
  const navigate = useNavigate();

  const [forgotMode, setForgotMode] = useState(false);
  const [resetUsername, setResetUsername] = useState("");
  const [resetMessage, setResetMessage] = useState<string | null>(null);
  const [resetSubmitting, setResetSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const { token, refreshToken, role, mustChangePassword } = await authenticationService.login(username, password);
      // Stash the token immediately so the /auth/me lookup below is authorized.
      localStorage.setItem("microlims_token", token);
      const me = await authenticationService.me();
      login({ token, refreshToken, username, role, fullName: me.fullName, userId: me.userId, mustChangePassword });
      navigate("/dashboard");
    } catch {
      setError("Invalid username or password.");
    }
  };

  const handleForgotSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setResetSubmitting(true);
    try {
      // The endpoint's response may include the raw reset token (email
      // delivery isn't wired up yet) - never surface it here, only the
      // generic message, so it can't be used as an account-existence oracle.
      await authenticationService.requestPasswordReset(resetUsername);
    } catch {
      // Swallow - same generic message either way.
    } finally {
      setResetSubmitting(false);
      setResetMessage("If that account exists, a reset link has been generated.");
    }
  };

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", bgcolor: "background.default" }}>
      <Box sx={{ width: 380, borderRadius: 2.5, overflow: "hidden", boxShadow: "0 4px 20px rgba(0,0,0,0.12)" }}>
        <Box sx={{ background: brandColors.topbarGradient, color: "#fff", px: 3, py: 2.5, textAlign: "center" }}>
          <Typography sx={{ fontSize: 22, fontWeight: 700 }}>
            Micro<Box component="span" sx={{ fontWeight: 300, opacity: 0.85 }}>LIMS</Box>
          </Typography>
        </Box>

        {forgotMode ? (
          <Box component="form" onSubmit={handleForgotSubmit} sx={{ bgcolor: "#fff", p: 3.5, display: "flex", flexDirection: "column", gap: 2 }}>
            {resetMessage ? (
              <Alert severity="info">{resetMessage}</Alert>
            ) : (
              <>
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                  Enter your username and we'll generate a password reset request.
                </Typography>
                <TextField label="Username" value={resetUsername} onChange={(e) => setResetUsername(e.target.value)} autoFocus required />
                <Button type="submit" variant="contained" size="large" disabled={resetSubmitting}>
                  {resetSubmitting ? "Submitting..." : "Send Reset Link"}
                </Button>
              </>
            )}
            <Link
              component="button"
              type="button"
              underline="hover"
              sx={{ fontSize: 13, textAlign: "center" }}
              onClick={() => { setForgotMode(false); setResetMessage(null); setResetUsername(""); }}
            >
              Back to login
            </Link>
          </Box>
        ) : (
          <Box component="form" onSubmit={handleSubmit} sx={{ bgcolor: "#fff", p: 3.5, display: "flex", flexDirection: "column", gap: 2 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField label="Username" value={username} onChange={(e) => setUsername(e.target.value)} autoFocus />
            <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            <Button type="submit" variant="contained" size="large">Login</Button>
            <Link component="button" type="button" underline="hover" sx={{ fontSize: 13, textAlign: "center" }} onClick={() => setForgotMode(true)}>
              Forgot password?
            </Link>
          </Box>
        )}
      </Box>
    </Box>
  );
}
