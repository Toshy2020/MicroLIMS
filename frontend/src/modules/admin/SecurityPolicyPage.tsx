import { useEffect, useState } from "react";
import { Alert, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { SecurityPolicy, SecurityPolicyService } from "./services/SecurityPolicyService";

// Read-only: shows the values the system enforces right now and where each
// one comes from. Changing them is a code or configuration change.
export function SecurityPolicyPage() {
  const [policy, setPolicy] = useState<SecurityPolicy | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    SecurityPolicyService.get().then(setPolicy).catch(() => setError("Could not load the security policy. Reload the page to try again."));
  }, []);

  return (
    <>
      <PageHeader title="Security Policy" subtitle="The password, lockout, session and e-signature rules in force. Read-only." />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!policy && !error && <LoadingSpinner />}
      {policy?.groups.map((group) => (
        <Paper key={group.key} sx={{ mb: 2, p: 2 }}>
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>{group.title}</Typography>
          <TableContainer>
            <Table size="small" aria-label={group.title}>
              <TableHead>
                <TableRow>
                  <TableCell>Setting</TableCell>
                  <TableCell>Value</TableCell>
                  <TableCell>Source</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {group.settings.map((s) => (
                  <TableRow key={s.key}>
                    <TableCell>{s.label}</TableCell>
                    <TableCell>{s.value}</TableCell>
                    <TableCell>{s.source}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      ))}
    </>
  );
}
