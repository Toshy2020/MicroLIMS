import { useState, useEffect, useMemo } from "react";
import type { SxProps, Theme } from "@mui/material";
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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { assignedAnalystNames } from "../utils/assignedAnalysts";
import axios from "axios";
import { getErrorMessage } from "../../../utils/errorMessage";

interface AssignAnalystDialogProps {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onAssigned: (updatedSample: SampleRecord) => void;
}

export const UNASSIGNED = "unassigned";

// Mirrors SampleAssignmentService's non-assignable statuses: these tests keep their analyst.
const LOCKED_STATUSES = ["ResultEntered", "Reviewed", "Approved", "Rejected", "Voided", "Cancelled", "RetestRequested"];

const valueOf = (analystId: number | null) => (analystId == null ? UNASSIGNED : String(analystId));

export const userLabel = (u: AssignableUser) => `${u.fullName} (${u.roleName})`;

export interface AnalystPickerProps {
  labelId: string;
  label: string;
  value: string;
  users: AssignableUser[];
  onChange: (value: string) => void;
  unassignedLabel?: string;
  disabled?: boolean;
  sx?: SxProps<Theme>;
}

export function AnalystPicker({
  labelId,
  label,
  value,
  users,
  onChange,
  unassignedLabel,
  disabled = false,
  sx
}: AnalystPickerProps) {
  return (
    <FormControl fullWidth size="small" disabled={disabled} sx={sx}>
      <InputLabel id={labelId}>{label}</InputLabel>
      <Select
        labelId={labelId}
        label={label}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      >
        {unassignedLabel && (
          <MenuItem value={UNASSIGNED}>
            <em>{unassignedLabel}</em>
          </MenuItem>
        )}
        {users.map((u) => (
          <MenuItem key={u.id} value={String(u.id)}>
            {userLabel(u)}
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );
}

// Each test on the sample carries its own analyst, so the dialog has one
// picker per test plus a shortcut that fills every test the chosen person
// may take. Only the choices the server returned are offered; it re-checks
// every rule (section membership, Section Heads assign only themselves).
// The server answered and said no (a business rule such as "Tests with a result cannot be reassigned."):
// repeating the request gives the same answer. A missing response, a timeout, throttling or a 5xx may pass on retry.
function isPermanentRefusal(err: unknown): boolean {
  if (!axios.isAxiosError(err) || !err.response) return false;
  const { status } = err.response;
  return status >= 400 && status < 500 && status !== 408 && status !== 429;
}

export function AssignAnalystDialog({
  open,
  sample,
  onClose,
  onAssigned
}: AssignAnalystDialogProps) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  const [options, setOptions] = useState<SampleAssignmentOptions | null>(null);
  const [selections, setSelections] = useState<Record<number, string>>({});
  const [allTestsValue, setAllTestsValue] = useState<string>("");
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // What the Retry button repeats; null when the refusal is final and only Close is offered.
  const [retryAction, setRetryAction] = useState<"load" | "save" | null>(null);

  const loadOptions = async () => {
    if (!sample) return;
    setError(null);
    setRetryAction(null);
    setLoading(true);
    try {
      const loaded = await ReceiveService.getAssignmentOptions(sample.sampleId);
      setOptions(loaded);
      setSelections(Object.fromEntries(loaded.tests.map((test) => [test.testOrderId, valueOf(test.assignedAnalystId)])));
    } catch (err: unknown) {
      setOptions(null);
      setRetryAction(isPermanentRefusal(err) ? null : "load");
      setError(
        getErrorMessage(
          err,
          t("assignAnalyst.loadFailed")
        )
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
    setRetryAction(null);
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
    } catch (err: unknown) {
      setRetryAction(isPermanentRefusal(err) ? null : "save");
      setError(getErrorMessage(err, t("assignAnalyst.saveFailed")));
    } finally {
      setSubmitting(false);
    }
  };

  if (!sample) return null;

  const currentNames = assignedAnalystNames(sample.assignedTests);
  const hasAssigned = currentNames.length > 0;
  const currentAssignedName = hasAssigned ? currentNames.join(", ") : t("assignAnalyst.unassigned");
  const assignableToAny = (options?.users ?? []).filter((u) => assignableTests.some((test) => test.eligibleUserIds.includes(u.id)));

  const renderTestRow = (test: AssignableTest) => {
    const value = selections[test.testOrderId] ?? valueOf(test.assignedAnalystId);
    const eligible = test.eligibleUserIds.map((id) => usersById.get(id)).filter((u): u is AssignableUser => !!u);
    // Whoever is on the test now stays visible even when the signed-in user
    // could not pick them (e.g. another Section Head).
    const keepsCurrent = test.assignedAnalystId != null && !test.eligibleUserIds.includes(test.assignedAnalystId);

    return (
      <Box
        key={test.testOrderId}
        sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 1 }}
        data-testid={`assign-row-${test.testCode}`}
      >
        <Box sx={{ width: 150, flexShrink: 0 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}><Ltr>{test.testCode}</Ltr></Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap title={test.sectionName}>
            {test.sectionName}
          </Typography>
        </Box>
        {test.canAssign ? (
          <FormControl fullWidth size="small">
            <Select
              value={value}
              onChange={(e) => setSelections((prev) => ({ ...prev, [test.testOrderId]: e.target.value }))}
              inputProps={{ "aria-label": t("assignAnalyst.analystForTestAria", { testCode: test.testCode }) }}
            >
              <MenuItem value={UNASSIGNED}>
                <em>{t("assignAnalyst.unassigned")}</em>
              </MenuItem>
              {keepsCurrent && (
                <MenuItem value={String(test.assignedAnalystId)} disabled>
                  {test.assignedAnalystName}
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
              {test.assignedAnalystName ?? t("assignAnalyst.unassigned")}
            </Typography>
            <Chip size="small" label={LOCKED_STATUSES.includes(test.status) ? test.status : t("assignAnalyst.notYourLab")} />
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
            {t("assignAnalyst.title")}
          </Typography>
        </>
      }
      actions={
        <>
          <Button onClick={onClose} disabled={submitting} variant="outlined">
            {t("assignAnalyst.cancel")}
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={submitting || loading || !options}
            variant="contained"
            startIcon={submitting ? <CircularProgress size={16} /> : <AssignmentIndIcon />}
          >
            {submitting ? t("assignAnalyst.saving") : t("assignAnalyst.save")}
          </Button>
        </>
      }
    >
        {error && (
          <Alert
            severity="error"
            sx={{ mb: 2 }}
            action={
              retryAction ? (
                <Button color="inherit" size="small" onClick={retryAction === "load" ? loadOptions : handleSubmit}>
                  {t("assignAnalyst.retry")}
                </Button>
              ) : (
                <Button color="inherit" size="small" onClick={onClose}>
                  {t("assignAnalyst.close")}
                </Button>
              )
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
              <span>{t("assignAnalyst.referenceNumber")}</span>
              <strong><Ltr>{sample.referenceNumber}</Ltr></strong>
            </Box>
            {sample.batchNumber && (
              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <span>{t("assignAnalyst.batchControl")}</span>
                <strong><Ltr>{sample.batchNumber}</Ltr> / <Ltr>{sample.controlNumber || "—"}</Ltr></strong>
              </Box>
            )}
            <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2 }}>
              <span>{t("assignAnalyst.currentAssignment")}</span>
              <strong style={{ textAlign: "end", color: !hasAssigned ? theme.palette.text.disabled : theme.palette.primary.main }}>
                {currentAssignedName}
              </strong>
            </Box>
          </Stack>
        </Box>

        <Typography sx={{ fontSize: 12.5, color: "text.secondary", mb: 2 }}>
          {t("assignAnalyst.explanation")}
        </Typography>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : options && (
          <>
            {assignableTests.length > 1 && (
              <AnalystPicker
                labelId="assign-all-tests-label"
                label={t("assignAnalyst.assignAllLabel")}
                value={allTestsValue}
                users={assignableToAny}
                onChange={applyToAllTests}
                unassignedLabel={t("assignAnalyst.assignAllUnassigned")}
                sx={{ mb: 1 }}
              />
            )}
            {assignableTests.length === 0 && (
              <Alert severity="info" sx={{ mb: 1 }}>
                {t("assignAnalyst.noOpenTests")}
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
            label={t("assignAnalyst.reasonLabel")}
            placeholder={t("assignAnalyst.reasonPlaceholder")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            multiline
            rows={2}
          />
        </Box>
    </FloatingDialog>
  );
}
