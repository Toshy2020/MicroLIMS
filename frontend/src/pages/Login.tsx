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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const { token, role } = await authenticationService.login(username, password);
      login(token, username, role);
      navigate("/dashboard");
    } catch {
      setError("Invalid username or password.");
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
        <Box component="form" onSubmit={handleSubmit} sx={{ bgcolor: "#fff", p: 3.5, display: "flex", flexDirection: "column", gap: 2 }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField label="Username" value={username} onChange={(e) => setUsername(e.target.value)} autoFocus />
          <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          <Button type="submit" variant="contained" size="large">Login</Button>
          <Link href="#" underline="hover" sx={{ fontSize: 13, textAlign: "center" }}>Forgot password?</Link>
        </Box>
      </Box>
    </Box>
  );
}
