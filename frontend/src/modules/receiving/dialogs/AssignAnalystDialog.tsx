import React, { useState, useEffect, useMemo } from "react";
import {
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Typography,
  Box,
  Alert,
  Chip,
  CircularProgress,
  Divider,
  Stack,
  useTheme
} from "@mui/material";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import { AssignableTest, AssignableUser, SampleAssignmentOptions, SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { CategoryBadge } from "../../../components/StatusBadge";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { assignedAnalystNames } from "../utils/assignedAnalysts";

interface AssignAnalystDialogProps {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onAssigned: (updatedSample: SampleRecord) => void;
}

const UNASSIGNED = "unassigned";

// Mirrors SampleAssignmentService's closed statuses: these tests keep their analyst.
const CLOSED_STATUSES = ["Approved", "Rejected", "Voided", "Cancelled"];

const valueOf = (analystId: number | null) => (analystId == null ? UNASSIGNED : String(analystId));

const userLabel = (u: AssignableUser) => `${u.fullName} (${u.roleName})`;

// Each test on the sample carries its own analyst, so the dialog has one
// picker per test plus a shortcut that fills every test the chosen person
// may take. Only the choices the server returned are offered; it re-checks
// every rule (section membership, Section Heads assign only themselves).
export function AssignAnalystDialog({
  open,
  sample,
  onClose,
  onAssigned
}: AssignAnalystDialogProps) {
  const theme = useTheme();
  const [options, setOptions] = useState<SampleAssignmentOptions | null>(null);
  const [selections, setSelections] = useState<Record<number, string>>({});
  const [allTestsValue, setAllTestsValue] = useState<string>("");
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadOptions = async () => {
    if (!sample) return;
    setError(null);
    setLoading(true);
    try {
      const loaded = await ReceiveService.getAssignmentOptions(sample.sampleId);
      setOptions(loaded);
      setSelections(Object.fromEntries(loaded.tests.map((t) => [t.testOrderId, valueOf(t.assignedAnalystId)])));
    } catch (err: any) {
      setOptions(null);
      setError(
        err?.response?.data?.message ||
        "Unable to load eligible analysts. Please retry or contact an administrator."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open && sample) {
      setReason("");
      setAllTestsValue("");
      loadOptions();
    }
  }, [open, sample]);

  const usersById = useMemo(
    () => new Map((options?.users ?? []).map((u) => [u.id, u])),
    [options]
  );
  const assignableTests = useMemo(() => (options?.tests ?? []).filter((t) => t.canAssign), [options]);

  const changes = assignableTests.filter((t) => selections[t.testOrderId] !== valueOf(t.assignedAnalystId));

  const applyToAllTests = (value: string) => {
    setAllTestsValue(value);
    setSelections((prev) => {
      const next = { ...prev };
      for (const t of assignableTests) {
        if (value === UNASSIGNED || t.eligibleUserIds.includes(Number(value))) {
          next[t.testOrderId] = value;
        }
      }
      return next;
    });
  };

  const handleSubmit = async () => {
    if (!sample) return;
    if (changes.length === 0) {
      onClose();
      return;
    }
    setError(null);
    setSubmitting(true);

    try {
      const updated = await ReceiveService.assignTestAnalysts(
        sample.sampleId,
        changes.map((t) => ({
          testOrderId: t.testOrderId,
          analystUserId: selections[t.testOrderId] === UNASSIGNED ? null : Number(selections[t.testOrderId])
        })),
        reason.trim() || undefined
      );
      onAssigned(updated);
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || "Failed to assign analyst.");
    } finally {
      setSubmitting(false);
    }
  };

  if (!sample) return null;

  const currentNames = assignedAnalystNames(sample.assignedTests);
  const currentAssignedName = currentNames.length > 0 ? currentNames.join(", ") : "Unassigned";
  const assignableToAny = (options?.users ?? []).filter((u) => assignableTests.some((t) => t.eligibleUserIds.includes(u.id)));

  const renderTestRow = (t: AssignableTest) => {
    const value = selections[t.testOrderId] ?? valueOf(t.assignedAnalystId);
    const eligible = t.eligibleUserIds.map((id) => usersById.get(id)).filter((u): u is AssignableUser => !!u);
    // Whoever is on the test now stays visible even when the signed-in user
    // could not pick them (e.g. another Section Head).
    const keepsCurrent = t.assignedAnalystId != null && !t.eligibleUserIds.includes(t.assignedAnalystId);

    return (
      <Box
        key={t.testOrderId}
        sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 1 }}
        data-testid={`assign-row-${t.testCode}`}
      >
        <Box sx={{ width: 150, flexShrink: 0 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>{t.testCode}</Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap title={t.sectionName}>
            {t.sectionName}
          </Typography>
        </Box>
        {t.canAssign ? (
          <FormControl fullWidth size="small">
            <Select
              value={value}
              onChange={(e) => setSelections((prev) => ({ ...prev, [t.testOrderId]: e.target.value }))}
              inputProps={{ "aria-label": `Analyst for ${t.testCode}` }}
            >
              <MenuItem value={UNASSIGNED}>
                <em>Unassigned</em>
              </MenuItem>
              {keepsCurrent && (
                <MenuItem value={String(t.assignedAnalystId)} disabled>
                  {t.assignedAnalystName}
                </MenuItem>
              )}
              {eligible.map((u) => (
                <MenuItem key={u.id} value={String(u.id)}>
                  {userLabel(u)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        ) : (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }} noWrap>
              {t.assignedAnalystName ?? "Unassigned"}
            </Typography>
            <Chip size="small" label={CLOSED_STATUSES.includes(t.status) ? t.status : "Not your laboratory"} />
          </Box>
        )}
      </Box>
    );
  };

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
            Assign Analysts
          </Typography>
        </>
      }
      actions={
        <>
          <Button onClick={onClose} disabled={submitting} variant="outlined">
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={submitting || loading || !options}
            variant="contained"
            startIcon={submitting ? <CircularProgress size={16} /> : <AssignmentIndIcon />}
          >
            {submitting ? "Saving..." : "Save Assignment"}
          </Button>
        </>
      }
    >
        {error && (
          <Alert
            severity="error"
            sx={{ mb: 2 }}
            action={
              <Button color="inherit" size="small" onClick={loadOptions}>
                Retry
              </Button>
            }
          >
            {error}
          </Alert>
        )}

        {/* Sample Metadata Card */}
        <Box
          sx={{
            p: 2,
            mb: 2.5,
            bgcolor: "background.default",
            borderRadius: 1.5,
            border: 1,
            borderColor: "divider"
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
              {sample.displayName}
            </Typography>
            <CategoryBadge category={sample.category} />
          </Box>
          <Stack spacing={0.5} sx={{ fontSize: 12, color: "text.secondary" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between" }}>
              <span>Reference Number:</span>
              <strong>{sample.referenceNumber}</strong>
            </Box>
            {sample.batchNumber && (
              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <span>Batch / Control No.:</span>
                <strong>{sample.batchNumber} / {sample.controlNumber || "—"}</strong>
              </Box>
            )}
            <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2 }}>
              <span>Current Assignment:</span>
              <strong style={{ textAlign: "right", color: currentAssignedName === "Unassigned" ? theme.palette.text.disabled : theme.palette.primary.main }}>
                {currentAssignedName}
              </strong>
            </Box>
          </Stack>
        </Box>

        <Typography sx={{ fontSize: 12.5, color: "text.secondary", mb: 2 }}>
          Each test can have its own analyst. Analysts, Reviewers and Section Heads of the test's laboratory can be
          assigned; a Section Head can only assign themselves. The analyst on a test prepares and performs it.
        </Typography>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : options && (
          <>
            {assignableTests.length > 1 && (
              <FormControl fullWidth size="small" sx={{ mb: 1 }}>
                <InputLabel id="assign-all-tests-label">Assign all tests to</InputLabel>
                <Select
                  labelId="assign-all-tests-label"
                  label="Assign all tests to"
                  value={allTestsValue}
                  onChange={(e) => applyToAllTests(e.target.value)}
                >
                  <MenuItem value={UNASSIGNED}>
                    <em>Unassigned (Open for any authorized analyst to prepare)</em>
                  </MenuItem>
                  {assignableToAny.map((u) => (
                    <MenuItem key={u.id} value={String(u.id)}>
                      {userLabel(u)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            {assignableTests.length === 0 && (
              <Alert severity="info" sx={{ mb: 1 }}>
                This sample has no open tests in your laboratories to assign.
              </Alert>
            )}
            <Divider />
            {options.tests.map(renderTestRow)}
          </>
        )}

        <Box sx={{ mt: 2.5 }}>
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
        </Box>
    </FloatingDialog>
  );
}
