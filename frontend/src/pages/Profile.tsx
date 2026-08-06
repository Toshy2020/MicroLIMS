import { useEffect, useState } from "react";
import { Paper, Typography, Box, Alert } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";
import { LoadingSpinner } from "../components/LoadingSpinner";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { CurrentUserInfo } from "../modules/authentication/types/authTypes";

function Field({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ mb: 1.5 }}>
      <Typography sx={{ fontSize: 11, color: "#9ca3af" }}>{label}</Typography>
      <Typography sx={{ fontWeight: 600 }}>{value}</Typography>
    </Box>
  );
}

// Read-only - in a GMP system, users don't self-serve name/role changes;
// an administrator does that via the Users module.
export function ProfilePage() {
  const [info, setInfo] = useState<CurrentUserInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    authenticationService.me().then(setInfo).catch(() => setError("Could not load profile."));
  }, []);

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!info) return <LoadingSpinner />;

  return (
    <>
      <PageHeader title="Profile" />
      <SectionTitle>Account</SectionTitle>
      <Paper sx={{ p: 2.5, maxWidth: 400 }}>
        <Field label="Full Name" value={info.fullName} />
        <Field label="Username" value={info.username} />
        <Field label="Role" value={info.role} />
        <Field label="Last Login" value={info.lastLoginAt ? new Date(info.lastLoginAt).toLocaleString() : "Never"} />
        <Field label="Password Last Changed" value={info.passwordChangedAt ? new Date(info.passwordChangedAt).toLocaleString() : "Never"} />
      </Paper>
    </>
  );
}
