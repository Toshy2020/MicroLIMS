import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { workflowProfileService } from "../services/workflowProfileService";
import {
  profileErrorMessage,
  workflowTypeLabel,
  type LiveWorkflowType,
  type WorkflowProfileVersion
} from "../types/workflowProfileTypes";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";

interface ProfileHistoryDrawerProps {
  open: boolean;
  onClose: () => void;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  onChanged?: () => void;
}

const fmt = (n: number | null) => (n === null ? "-" : String(n));

// Every version of one scope, newest first. A test shows all of its own rows whatever
// type they were written under (the test may have been re-typed since).
export function ProfileHistoryDrawer({ open, onClose, workflowType, testCode, onChanged }: ProfileHistoryDrawerProps) {
  const canConfigure = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [rows, setRows] = useState<WorkflowProfileVersion[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [signing, setSigning] = useState<WorkflowProfileVersion | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await workflowProfileService.list(testCode ? { testCode } : { workflowType, defaultOnly: true }));
    } catch (e) {
      setError(profileErrorMessage(e, "Could not load the version history."));
    } finally {
      setLoading(false);
    }
  }, [workflowType, testCode]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  return (
    <>
      <Drawer anchor="right" open={open} onClose={onClose}>
        <Box sx={{ width: { xs: "100vw", sm: 720 }, p: 2 }}>
          <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 2 }}>
            <Typography variant="h6">History: {testCode ?? `${workflowTypeLabel(workflowType)} default`}</Typography>
            <IconButton aria-label="Close history" onClick={onClose}>
              <CloseIcon />
            </IconButton>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {loading && <CircularProgress size={24} />}
          {!loading && !error && rows.length === 0 && (
            <Typography color="text.secondary">No versions yet.</Typography>
          )}

          {rows.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Effective from</TableCell>
                  <TableCell align="right">Work units</TableCell>
                  <TableCell align="right">Target (days)</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Reason</TableCell>
                  <TableCell>By</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.effectiveFrom}</TableCell>
                    <TableCell align="right">{fmt(r.workUnits)}</TableCell>
                    <TableCell align="right">{fmt(r.targetAnalystDays)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={r.status} color={r.status === "Effective" ? "success" : "default"} />
                    </TableCell>
                    <TableCell>{r.reasonForChange}</TableCell>
                    <TableCell>
                      {r.createdByName}
                      {r.signedAt && (
                        <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">
                          signed {r.signedAt.slice(0, 10)}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      {canConfigure && r.status === "Draft" && (
                        <Button size="small" onClick={() => setSigning(r)}>
                          Review and sign
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      </Drawer>

      {signing && (
        <AddProfileVersionDialog
          open
          draft={signing}
          workflowType={signing.workflowType}
          testCode={signing.testCode}
          onClose={() => setSigning(null)}
          onDone={() => {
            void load();
            onChanged?.();
          }}
        />
      )}
    </>
  );
}
