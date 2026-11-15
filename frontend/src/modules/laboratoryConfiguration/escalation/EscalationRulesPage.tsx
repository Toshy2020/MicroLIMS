import { useCallback, useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import axios from "axios";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControlLabel,
  Link as MuiLink,
  MenuItem,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { LabPage } from "../../../components/lab";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { APP_ROUTES } from "../../../routes/routes";
import { type LaboratorySection, getMySections } from "../../../services/laboratorySectionService";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatLabDateTime } from "../../../utils/formatDate";
import { formatRoleName } from "../../../utils/userDisplay";
import { EscalationRuleDialog } from "./EscalationRuleDialog";
import {
  type EscalationRuleDto,
  escalationRulesService,
  formatTriggerLabel
} from "./services/escalationRulesService";
import { type WorkingHoursDto, workingHoursService } from "./services/workingHoursService";

export function EscalationRulesPage() {
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);
  const [workingHours, setWorkingHours] = useState<WorkingHoursDto | null>(null);
  const [bootstrapLoading, setBootstrapLoading] = useState(true);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);
  const [bootstrapReloadToken, setBootstrapReloadToken] = useState(0);

  const [includeRetired, setIncludeRetired] = useState(false);
  const [rules, setRules] = useState<EscalationRuleDto[]>([]);
  const [rulesLoading, setRulesLoading] = useState(false);
  const [rulesError, setRulesError] = useState<string | null>(null);
  const [rulesReloadToken, setRulesReloadToken] = useState(0);

  const [dialogMode, setDialogMode] = useState<"create" | "replace" | null>(null);
  const [editingRule, setEditingRule] = useState<EscalationRuleDto | null>(null);
  const [retireTarget, setRetireTarget] = useState<EscalationRuleDto | null>(null);
  const [retiring, setRetiring] = useState(false);

  const loadBootstrap = useCallback((signal: AbortSignal) => {
    setBootstrapLoading(true);
    setBootstrapError(null);

    Promise.all([getMySections(signal), workingHoursService.get(signal)])
      .then(([loadedSections, loadedHours]) => {
        if (signal.aborted) return;
        setSections(loadedSections);
        setWorkingHours(loadedHours);
        setSelectedSectionId((prev) => {
          if (prev !== null && loadedSections.some((s) => s.sectionId === prev)) {
            return prev;
          }
          return loadedSections.length > 0 ? loadedSections[0].sectionId : null;
        });
        setBootstrapLoading(false);
      })
      .catch((err: unknown) => {
        if (signal.aborted || axios.isCancel(err)) return;
        const message = getErrorMessage(err, "Could not load escalation configuration.");
        setBootstrapError(message);
        setBootstrapLoading(false);
        toast.error(message);
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadBootstrap(controller.signal);
    return () => controller.abort();
  }, [loadBootstrap, bootstrapReloadToken]);

  useEffect(() => {
    if (bootstrapLoading || bootstrapError || selectedSectionId === null) {
      setRules([]);
      setRulesLoading(false);
      setRulesError(null);
      return;
    }

    const controller = new AbortController();
    setRulesLoading(true);
    setRulesError(null);

    escalationRulesService
      .list({ sectionId: selectedSectionId, includeRetired }, controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        setRules(data);
        setRulesLoading(false);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted || axios.isCancel(err)) return;
        const message = getErrorMessage(err, "Could not load escalation rules.");
        setRulesError(message);
        setRulesLoading(false);
        toast.error(message);
      });

    return () => controller.abort();
  }, [bootstrapLoading, bootstrapError, selectedSectionId, includeRetired, rulesReloadToken]);

  const selectedSection = useMemo(
    () => sections.find((s) => s.sectionId === selectedSectionId) ?? null,
    [sections, selectedSectionId]
  );

  const replacedRuleIds = useMemo(() => {
    const set = new Set<number>();
    for (const rule of rules) {
      if (rule.replacesRuleId !== null) {
        set.add(rule.replacesRuleId);
      }
    }
    return set;
  }, [rules]);

  const workingHoursConfigured = workingHours?.isConfigured ?? false;

  const handleConfirmRetire = async () => {
    if (!retireTarget || retiring) return;
    setRetiring(true);
    try {
      await escalationRulesService.retire(retireTarget.id);
      toast.success("Escalation rule retired.");
      setRetireTarget(null);
      setRulesReloadToken((t) => t + 1);
    } catch (err: unknown) {
      const message = getErrorMessage(err, "Could not retire escalation rule.");
      toast.error(message);
    } finally {
      setRetiring(false);
    }
  };

  return (
    <LabPage
      title="Escalation Rules"
      subtitle="Notify section leads automatically when waiting work exceeds configured working-hours thresholds."
      actions={
        selectedSection && workingHoursConfigured ? (
          <Button
            variant="contained"
            startIcon={<AddOutlinedIcon />}
            onClick={() => {
              setEditingRule(null);
              setDialogMode("create");
            }}
          >
            Add rule
          </Button>
        ) : null
      }
    >
      {bootstrapLoading && (
        <Paper sx={{ p: 2.5 }}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }} role="status" aria-live="polite">
            <CircularProgress size={20} />
            <Typography variant="body2" color="text.secondary">
              Loading escalation configuration...
            </Typography>
          </Stack>
        </Paper>
      )}

      {!bootstrapLoading && bootstrapError && (
        <LoadErrorAlert
          message={bootstrapError}
          onRetry={() => setBootstrapReloadToken((t) => t + 1)}
        />
      )}

      {!bootstrapLoading && !bootstrapError && sections.length === 0 && (
        <Paper sx={{ p: 2.5 }}>
          <Alert severity="info">
            You are not assigned to any laboratory sections.
          </Alert>
        </Paper>
      )}

      {!bootstrapLoading && !bootstrapError && sections.length > 0 && (
        <Stack spacing={2}>
          {!workingHoursConfigured && (
            <Alert severity="warning" role="alert">
              Working hours must be configured before escalation rules can be added or changed. Set at least one working day on the{" "}
              <MuiLink component={RouterLink} to={APP_ROUTES.WORKING_HOURS} underline="hover">
                working hours calendar
              </MuiLink>{" "}
              first.
            </Alert>
          )}

          <Paper sx={{ p: 2.5 }}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={2}
              sx={{ alignItems: { sm: "center" }, justifyContent: "space-between", mb: 2 }}
            >
              <TextField
                select
                size="small"
                label="Section"
                value={selectedSectionId ?? ""}
                onChange={(e) => setSelectedSectionId(Number(e.target.value))}
                sx={{ minWidth: 260 }}
              >
                {sections.map((section) => (
                  <MenuItem key={section.sectionId} value={section.sectionId}>
                    {section.sectionName}
                  </MenuItem>
                ))}
              </TextField>

              <FormControlLabel
                control={
                  <Switch
                    checked={includeRetired}
                    onChange={(e) => setIncludeRetired(e.target.checked)}
                    slotProps={{ input: { "aria-label": "Show retired" } }}
                  />
                }
                label="Show retired"
              />
            </Stack>

            {rulesLoading && (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 3 }} role="status" aria-live="polite">
                <CircularProgress size={20} />
                <Typography variant="body2" color="text.secondary">
                  Loading escalation rules...
                </Typography>
              </Stack>
            )}

            {!rulesLoading && rulesError && (
              <LoadErrorAlert
                message={rulesError}
                onRetry={() => setRulesReloadToken((t) => t + 1)}
              />
            )}

            {!rulesLoading && !rulesError && rules.length === 0 && (
              <Box sx={{ py: 3, textAlign: "center" }}>
                <Typography variant="body2" color="text.secondary">
                  {includeRetired
                    ? "No escalation rules configured for this section yet."
                    : "No active escalation rules configured for this section."}
                </Typography>
              </Box>
            )}

            {!rulesLoading && !rulesError && rules.length > 0 && (
              <TableContainer>
                <Table aria-label="Escalation rules" size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Trigger</TableCell>
                      <TableCell>Threshold</TableCell>
                      <TableCell>Notify role</TableCell>
                      <TableCell>Status</TableCell>
                      <TableCell align="right">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rules.map((rule) => {
                      const isActive = rule.isActive && rule.retiredAt === null;
                      const statusLabel = isActive
                        ? "Active"
                        : replacedRuleIds.has(rule.id)
                          ? "Replaced"
                          : "Retired";
                      return (
                        <TableRow key={rule.id}>
                          <TableCell>
                            <Typography variant="body2" sx={{ fontWeight: 500 }}>
                              {formatTriggerLabel(rule.trigger)}
                            </Typography>
                            {rule.replacesRuleId !== null && (
                              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                                Replaces earlier rule
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>
                            {rule.thresholdWorkingHours} working hours
                          </TableCell>
                          <TableCell>{formatRoleName(rule.notifyRoleType)}</TableCell>
                          <TableCell>
                            <Chip
                              label={statusLabel}
                              size="small"
                              color={isActive ? "success" : "default"}
                              variant={isActive ? "filled" : "outlined"}
                            />
                            {rule.retiredAt ? (
                              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                                Retired {formatLabDateTime(rule.retiredAt)}
                                {rule.retiredByName ? ` by ${rule.retiredByName}` : ""}
                              </Typography>
                            ) : (
                              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                                Added {formatLabDateTime(rule.createdAt)}
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell align="right">
                            {isActive && (
                              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                                <Button
                                  size="small"
                                  disabled={!workingHoursConfigured}
                                  onClick={() => {
                                    setEditingRule(rule);
                                    setDialogMode("replace");
                                  }}
                                >
                                  Edit
                                </Button>
                                <Button
                                  size="small"
                                  color="error"
                                  onClick={() => setRetireTarget(rule)}
                                >
                                  Retire
                                </Button>
                              </Stack>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>
        </Stack>
      )}

      {selectedSection && (
        <EscalationRuleDialog
          open={dialogMode !== null}
          mode={dialogMode ?? "create"}
          sectionId={selectedSection.sectionId}
          sectionName={selectedSection.sectionName}
          existingRule={editingRule}
          onClose={() => {
            setDialogMode(null);
            setEditingRule(null);
          }}
          onSaved={() => {
            setDialogMode(null);
            setEditingRule(null);
            setRulesReloadToken((t) => t + 1);
          }}
        />
      )}

      <ConfirmationDialog
        destructive
        open={retireTarget !== null}
        title="Retire escalation rule"
        confirmText="Retire"
        cancelText="Cancel"
        loading={retiring}
        message={
          retireTarget
            ? `Retire "${formatTriggerLabel(retireTarget.trigger)}" (${retireTarget.thresholdWorkingHours} working hours to ${formatRoleName(retireTarget.notifyRoleType)})? Past escalation history stays in the audit log, and this rule will stop firing.`
            : ""
        }
        onCancel={() => setRetireTarget(null)}
        onConfirm={handleConfirmRetire}
      />
    </LabPage>
  );
}
