import type { ReactNode } from "react";
import {
  Alert, Box, Button, CircularProgress, Paper, Stack,
  Table, TableBody, TableCell, TableHead, TableRow, Typography, useTheme,
} from "@mui/material";
import { useTranslation } from "../../i18n";
import type { FieldError } from "../../services/apiTypes";
import { diffValues, type WizardDiffItem, type WizardSummaryItem } from "./diffValues";
import { WhereUsedPanel } from "./WhereUsedPanel";
import type { ConflictState, UsageLoadState, WizardStep } from "./useWizard";

export interface WizardReviewProps<M> {
  mode: "create" | "edit";
  steps: WizardStep<M>[];
  applicableSteps: WizardStep<M>[];
  model: M;
  original?: M;
  serverErrors: FieldError[];
  serverErrorsByStep: Record<string, Record<string, string>>;
  unmappedServerErrors: FieldError[];
  validatingServer: boolean;
  serverValidationError: string | null;
  onRetryServerValidation: () => void;
  hasUsage: boolean;
  usageState: UsageLoadState;
  usageBlocked: boolean;
  onRetryUsage: () => void;
  reasonRequired?: boolean;
  conflictState: ConflictState;
  onDismissConflict: () => void;
  saveError: string | null;
  onEditStep: (stepKey: string, focusField?: string) => void;
  extraReview?: (model: M) => ReactNode;
}

function collectSummary<M>(steps: WizardStep<M>[], target: M): WizardSummaryItem[] {
  const rows: WizardSummaryItem[] = [];
  for (const step of steps) {
    if (step.applies && !step.applies(target)) continue;
    rows.push(...step.summary(target));
  }
  return rows;
}

function DiffTable({ diffs, beforeHeader, afterHeader }: {
  diffs: WizardDiffItem[];
  beforeHeader: string;
  afterHeader: string;
}) {
  const theme = useTheme();
  const { t } = useTranslation();
  const headerSx = { color: theme.custom.chrome.tableHeaderText, fontWeight: 600 };
  return (
    <Table size="small" aria-label={t("components.wizardReview.changedFieldsAria")}>
      <TableHead>
        <TableRow sx={{ bgcolor: theme.custom.chrome.tableHeaderBg }}>
          <TableCell sx={headerSx}>{t("components.wizardReview.field")}</TableCell>
          <TableCell sx={headerSx}>{beforeHeader}</TableCell>
          <TableCell sx={headerSx}>{afterHeader}</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {diffs.map((row) => (
          <TableRow key={row.label}>
            <TableCell sx={{ fontWeight: 500 }}>{row.label}</TableCell>
            <TableCell sx={{ color: "text.secondary" }}>{row.before || "—"}</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>{row.after || "—"}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Review step for ConfigWizard: displays per-step summaries with Edit links,
 * edit-mode before/after diffs, server validation results, where-used impact,
 * and 409 concurrency conflict recovery.
 */
export function WizardReview<M>({
  mode, steps, applicableSteps, model, original,
  serverErrors, serverErrorsByStep, unmappedServerErrors,
  validatingServer, serverValidationError, onRetryServerValidation,
  hasUsage, usageState, usageBlocked, onRetryUsage, reasonRequired,
  conflictState, onDismissConflict, saveError, onEditStep, extraReview,
}: WizardReviewProps<M>) {
  const { t } = useTranslation();
  const stepTitleByKey = new Map(applicableSteps.map((s) => [s.key, s.title]));
  const currentSummaryRows = collectSummary(steps, model);
  const originalSummaryRows = original ? collectSummary(steps, original) : [];
  const diffs = original ? diffValues(originalSummaryRows, currentSummaryRows) : [];

  return (
    <Stack spacing={2.5}>
      {saveError && <Alert severity="error">{saveError}</Alert>}

      {conflictState.active && (
        <Alert
          severity="warning"
          role="alert"
          action={
            <Button
              type="button"
              variant="contained"
              color="warning"
              size="small"
              onClick={onDismissConflict}
              disabled={conflictState.reloading}
            >
              {t("components.wizardReview.reviewAndSaveAgain")}
            </Button>
          }
        >
          <Stack spacing={1.5}>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {t("components.wizardReview.conflictMessage")}
            </Typography>
            {conflictState.reloading && (
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <CircularProgress size={16} />
                <Typography variant="body2">{t("components.wizardReview.loadingLatest")}</Typography>
              </Box>
            )}
            {conflictState.reloadError && (
              <Typography variant="body2" color="error">{conflictState.reloadError}</Typography>
            )}
            {!conflictState.reloading && original && (
              diffs.length > 0 ? (
                <DiffTable diffs={diffs} beforeHeader={t("components.wizardReview.currentRecord")} afterHeader={t("components.wizardReview.yourInput")} />
              ) : (
                <Typography variant="body2">{t("components.wizardReview.noDifferences")}</Typography>
              )
            )}
          </Stack>
        </Alert>
      )}

      {validatingServer && (
        <Box role="status" aria-busy="true" sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <CircularProgress size={16} />
          <Typography variant="body2" color="text.secondary">{t("components.wizardReview.validating")}</Typography>
        </Box>
      )}

      {serverValidationError && (
        <Alert
          severity="error"
          action={
            <Button type="button" color="inherit" size="small" onClick={onRetryServerValidation}>
              {t("components.wizardReview.retry")}
            </Button>
          }
        >
          {serverValidationError}
        </Alert>
      )}

      {serverErrors.length > 0 && (
        <Alert severity="error" role="alert">
          <Stack spacing={0.5}>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {t("components.wizardReview.fixIssues")}
            </Typography>
            {Object.entries(serverErrorsByStep).map(([stepKey, fields]) =>
              Object.entries(fields).map(([field, msg]) => {
                const stepTitle = stepTitleByKey.get(stepKey) ?? stepKey;
                return (
                  <Box key={`${stepKey}-${field}`} sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                    <Typography variant="body2">{stepTitle}: {msg}</Typography>
                    <Button type="button" size="small" color="inherit" onClick={() => onEditStep(stepKey, field)}>
                      {t("components.wizardReview.goTo", { step: stepTitle })}
                    </Button>
                  </Box>
                );
              })
            )}
            {unmappedServerErrors.map((fe, idx) => (
              <Typography key={`${fe.field}-${idx}`} variant="body2">
                {fe.field ? `${fe.field}: ${fe.message}` : fe.message}
              </Typography>
            ))}
          </Stack>
        </Alert>
      )}

      {mode === "edit" && original && !conflictState.active && (
        <Box component="section" aria-label={t("components.wizardReview.changes")}>
          <Typography variant="subtitle2" component="h3" sx={{ mb: 1, fontWeight: 600 }}>
            {t("components.wizardReview.changes")}
          </Typography>
          {diffs.length > 0 ? (
            <Paper variant="outlined">
              <DiffTable diffs={diffs} beforeHeader={t("components.wizardReview.before")} afterHeader={t("components.wizardReview.after")} />
            </Paper>
          ) : (
            <Typography variant="body2" color="text.secondary">{t("components.wizardReview.noChanges")}</Typography>
          )}
        </Box>
      )}

      {applicableSteps.map((step) => {
        const rows = step.summary(model);
        const stepServerErrs = serverErrorsByStep[step.key] ?? {};
        const firstFocusField = Object.keys(stepServerErrs)[0] ?? rows[0]?.field;
        return (
          <Paper key={step.key} component="section" aria-label={step.title} variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
              <Typography variant="subtitle2" component="h3" sx={{ fontWeight: 600 }}>
                {step.title}
              </Typography>
              <Button type="button" size="small" onClick={() => onEditStep(step.key, firstFocusField)}>
                {t("components.wizardReview.edit")}
              </Button>
            </Box>
            <Stack spacing={0.75}>
              {rows.map((row) => (
                <Box key={row.field || row.label} sx={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 2 }}>
                  <Typography variant="body2" color="text.secondary">{row.label}</Typography>
                  <Box sx={{ textAlign: "right" }}>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>{row.value || "—"}</Typography>
                    {stepServerErrs[row.field] && (
                      <Typography variant="caption" color="error">{stepServerErrs[row.field]}</Typography>
                    )}
                  </Box>
                </Box>
              ))}
            </Stack>
          </Paper>
        );
      })}

      {hasUsage && (
        <Box>
          {usageState.status === "loading" && (
            <Box role="status" aria-busy="true" sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="body2" color="text.secondary">{t("components.wizardReview.checkingUsage")}</Typography>
            </Box>
          )}
          {usageState.status === "error" && (
            <Alert
              severity="error"
              action={
                <Button type="button" color="inherit" size="small" onClick={onRetryUsage}>
                  {t("components.wizardReview.retry")}
                </Button>
              }
            >
              {usageBlocked ? t("components.wizardReview.usageNotChecked") : usageState.error}
            </Alert>
          )}
          {usageState.status === "loaded" && usageState.summary && (
            <Stack spacing={1.5}>
              {reasonRequired && usageState.summary.openWorkCount > 0 && (
                <Alert severity="warning">
                  {t("components.wizardReview.openWork", { count: usageState.summary.openWorkCount })}
                </Alert>
              )}
              <WhereUsedPanel type={usageState.summary.type} id={usageState.summary.id} summary={usageState.summary} />
            </Stack>
          )}
        </Box>
      )}

      {extraReview?.(model)}
    </Stack>
  );
}
