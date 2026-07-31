import { Paper, Typography, Box } from "@mui/material";
import { useAuth } from "../contexts/AuthContext";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";

export function ProfilePage() {
  const { username, role } = useAuth();
  return (
    <>
      <PageHeader title="Profile" />
      <SectionTitle>Account</SectionTitle>
      <Paper sx={{ p: 2.5, maxWidth: 400 }}>
        <Box sx={{ mb: 1.5 }}>
          <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Username</Typography>
          <Typography sx={{ fontWeight: 600 }}>{username}</Typography>
        </Box>
        <Box>
          <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Role</Typography>
          <Typography sx={{ fontWeight: 600 }}>{role}</Typography>
        </Box>
      </Paper>
    </>
  );
}
