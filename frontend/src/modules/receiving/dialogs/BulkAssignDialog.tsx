import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Stack,
  TextField,
  Typography,
  useTheme
} from "@mui/material";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import { FloatingDialog } from "../../../components/FloatingDialog";
import type { BulkAction } from "../../../components/lab/table/BulkActionBar";
import type { RowSelection } from "../../../components/lab/table/useRowSelection";
import { PERMISSIONS } from "../../../routes/routes";
import { getErrorMessage } from "../../../utils/errorMessage";
import { ReceiveService } from "../services/ReceiveService";
import type { AssignableUser, BulkAssignResult } from "../types/receivingTypes";
import { AnalystPicker } from "./AssignAnalystDialog";

// How many selected samples are asked for their analysts before giving up.
const MAX_OPTION_PROBES = 10;

export interface BulkAssignDialogProps {
  open: boolean;
  sampleIds: number[];
  selection?: RowSelection<number | string>;
  onSelectionChange?: (remainingSampleIds: number[]) => void;
  onClose: () => void;
  onAssigned?: (result: BulkAssignResult) => void;
}

export function bulkAssignAction(
  onOpen: (sampleIds: number[]) => void
): BulkAction<string | number> {
  return {
    key: "assign-analyst",
    label: "Assign analyst",
    permission: PERMISSIONS.SAMPLES_ASSIGN_ANALYST,
    run: (ids) => {
      onOpen(ids.map((id) => Number(id)));
    }
  };
}

export const createBulkAssignAction = bulkAssignAction;

export function BulkAssignDialog({
  open,
  sampleIds,
  selection,
  onSelectionChange,
  onClose,
  onAssigned
}: BulkAssignDialogProps) {
  const theme = useTheme();
  const [users, setUsers] = useState<AssignableUser[]>([]);
  const [analystUserId, setAnalystUserId] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [loading, setLoading] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<BulkAssignResult | null>(null);

  // The analysts a sample can take come from that sample's own open tests, so a sample with none (voided, closed,
  // outside the user's sections) offers nobody. The list is taken from the first selected sample that offers
  // someone; the dialog says so when it had to skip samples, or when none of them offers anyone.
  const [notice, setNotice] = useState<{ severity: "info" | "warning"; text: string } | null>(null);
  const idsKey = sampleIds.join(",");
  const wasOpenRef = useRef(false);

  const loadAnalysts = useCallback(async () => {
    const probe = idsKey ? idsKey.split(",").map(Number).slice(0, MAX_OPTION_PROBES) : [];
    if (probe.length === 0) return;
    setError(null);
    setNotice(null);
    setLoading(true);
    try {
      let skipped = 0;
      let found: AssignableUser[] = [];
      let foundFor: number | null = null;
      for (const sampleId of probe) {
        const loaded = await ReceiveService.getAssignmentOptions(sampleId);
        if (loaded.users.length > 0) {
          found = loaded.users;
          foundFor = sampleId;
          break;
        }
        skipped += 1;
      }
      setUsers(found);
      if (foundFor === null) {
        setNotice({
          severity: "warning",
          text: `${probe.length === 1 ? "The selected sample has no" : `None of the first ${probe.length} selected samples has an`} open test you can assign in your laboratory sections (a voided or closed sample, or one outside your sections), so no analyst can be offered.`
        });
      } else if (skipped > 0) {
        setNotice({
          severity: "info",
          text: `${skipped} selected ${skipped === 1 ? "sample has" : "samples have"} no open test you can assign (for example a voided or closed sample). The analysts shown are those eligible for sample #${foundFor}. Each sample is still checked when you assign, and any refusal is listed afterwards.`
        });
      }
    } catch (err: unknown) {
      setUsers([]);
      setError(
        getErrorMessage(
          err,
          "Unable to load eligible analysts. Please retry or contact an administrator."
        )
      );
    } finally {
      setLoading(false);
    }
  }, [idsKey]);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setAnalystUserId("");
      setReason("");
      setError(null);
      setResult(null);
      void loadAnalysts();
    }
    wasOpenRef.current = open;
  }, [open, loadAnalysts]);

  const handleSubmit = async () => {
    if (!analystUserId || sampleIds.length === 0) return;
    setError(null);
    setSubmitting(true);

    try {
      const response = await ReceiveService.bulkAssign({
        sampleIds,
        analystUserId: Number(analystUserId),
        reason: reason.trim() || undefined
      });

      const refusedIds = response.rows
        .filter((row) => !row.assigned)
        .map((row) => row.sampleId);

      if (selection) {
        selection.clear();
        for (const id of refusedIds) {
          selection.toggle(id);
        }
      }
      onSelectionChange?.(refusedIds);
      onAssigned?.(response);
      setResult(response);
    } catch (err: unknown) {
      setError(getErrorMessage(err, "Failed to assign analyst to selected samples."));
    } finally {
      setSubmitting(false);
    }
  };

  if (!open) return null;

  const refusedRows = result ? result.rows.filter((r) => !r.assigned) : [];

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      titleSx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}
      title={
        <>
          <AssignmentIndIcon sx={{ color: theme.palette.primary.main }} />
          <Typography variant="h6" component="span" sx={{ fontWeight: 700 }}>
            {result ? "Bulk Assignment Results" : "Bulk Assign Analyst"}
          </Typography>
        </>
      }
      actions={
        result ? (
          <Button onClick={onClose} variant="contained">
            Close
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={submitting} variant="outlined">
              Cancel
            </Button>
            <Button
              onClick={handleSubmit}
              disabled={submitting || loading || !analystUserId || sampleIds.length === 0}
              variant="contained"
              startIcon={submitting ? <CircularProgress size={16} /> : <AssignmentIndIcon />}
            >
              {submitting ? "Assigning..." : "Assign Analyst"}
            </Button>
          </>
        )
      }
    >
      {error && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => void loadAnalysts()}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {result ? (
        <Stack spacing={2}>
          <Alert severity={result.refusedCount > 0 ? "warning" : "success"}>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {result.assignedCount} assigned, {result.refusedCount} refused
            </Typography>
          </Alert>

          {refusedRows.length > 0 && (
            <Box
              sx={{
                p: 1.5,
                bgcolor: "background.default",
                borderRadius: 1.5,
                border: 1,
                borderColor: "divider"
              }}
            >
              <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 700 }}>
                Refused Samples ({refusedRows.length})
              </Typography>
              <Divider sx={{ mb: 1 }} />
              <Stack spacing={1}>
                {refusedRows.map((row) => (
                  <Box
                    key={row.sampleId}
                    sx={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "flex-start",
                      gap: 2
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 700, flexShrink: 0 }}>
                      {row.referenceNumber}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ textAlign: "right" }}>
                      {row.refusedReason}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            </Box>
          )}
        </Stack>
      ) : (
        <Stack spacing={2}>
          <Box
            sx={{
              p: 1.5,
              bgcolor: "background.default",
              borderRadius: 1.5,
              border: 1,
              borderColor: "divider"
            }}
          >
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {sampleIds.length} {sampleIds.length === 1 ? "sample" : "samples"} selected
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Open tests in your laboratory sections on each selected sample will be assigned to the chosen analyst.
            </Typography>
          </Box>

          {notice && !loading && <Alert severity={notice.severity}>{notice.text}</Alert>}

          {loading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
              <CircularProgress size={28} />
            </Box>
          ) : (
            <AnalystPicker
              labelId="bulk-assign-analyst-label"
              label="Assign analyst"
              value={analystUserId}
              users={users}
              onChange={setAnalystUserId}
            />
          )}

          <TextField
            fullWidth
            size="small"
            label="Reason for Assignment / Reassignment (recorded in Audit Trail)"
            placeholder="e.g. Workload balancing, primary analyst on leave, sectional scheduling"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            multiline
            rows={2}
          />
        </Stack>
      )}
    </FloatingDialog>
  );
}
