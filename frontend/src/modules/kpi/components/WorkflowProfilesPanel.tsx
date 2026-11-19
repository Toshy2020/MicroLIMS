import { Fragment, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Collapse,
  FormControlLabel,
  IconButton,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useWorkflowProfiles } from "../hooks/useWorkflowProfiles";
import {
  workflowTypeLabel,
  type LiveWorkflowType,
  type ProfileCoverage,
  type WorkflowProfileTestRow,
  type WorkflowProfileTypeRow
} from "../types/workflowProfileTypes";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";
import { ProfileHistoryDrawer } from "./ProfileHistoryDrawer";
import { useTranslation } from "../../../i18n";

interface Scope {
  workflowType: LiveWorkflowType;
  testCode: string | null;
}

const COVERAGE_LABEL: Record<ProfileCoverage, string> = {
  Configured: "coverage.configured",
  Partial: "coverage.partial",
  NeedsConfiguration: "coverage.needsConfiguration"
};
const COVERAGE_COLOR: Record<ProfileCoverage, "success" | "warning" | "default"> = {
  Configured: "success",
  Partial: "warning",
  NeedsConfiguration: "default"
};


// Configuration only: effective work units and targets per workflow type, with per-test
// overrides underneath. It never shows analyst figures (no ranking, spec D6).
export function WorkflowProfilesPanel() {
  const { t } = useTranslation("kpi");
  const value = (n: number | null) => (n === null ? t("profiles.notSet") : String(n));
  const { overview, coverage, loading, error, reload } = useWorkflowProfiles();
  const canConfigure = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [onlyGaps, setOnlyGaps] = useState(false);
  const [expanded, setExpanded] = useState<Set<LiveWorkflowType>>(new Set());
  const [adding, setAdding] = useState<Scope | null>(null);
  const [history, setHistory] = useState<Scope | null>(null);

  const toggle = (type: LiveWorkflowType) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(type)) next.delete(type);
      else next.add(type);
      return next;
    });

  const needsWork = (c: ProfileCoverage) => c !== "Configured";
  const visibleTests = (row: WorkflowProfileTypeRow): WorkflowProfileTestRow[] =>
    onlyGaps ? row.tests.filter((test) => needsWork(test.coverage)) : row.tests;
  const rows = (overview?.types ?? []).filter(
    (test) => !onlyGaps || needsWork(test.coverage) || test.tests.some((x) => needsWork(x.coverage))
  );

  const gaps = coverage
    ? [
        t("gaps.typesWithoutUnits", { count: coverage.typesWithoutUnits.length }),
        t("gaps.typesWithoutTarget", { count: coverage.typesWithoutTarget.length }),
        t("gaps.testsNeedConfiguration", { count: coverage.testsNeedingConfiguration.length })
      ]
    : [];
  const hasGaps =
    !!coverage &&
    (coverage.typesWithoutUnits.length > 0 ||
      coverage.typesWithoutTarget.length > 0 ||
      coverage.testsNeedingConfiguration.length > 0);

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {t("profiles.intro")}
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {loading && !overview && <CircularProgress size={24} />}

      {hasGaps && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <FormControlLabel
              control={<Switch size="small" checked={onlyGaps} onChange={(e) => setOnlyGaps(e.target.checked)} />}
              label={t("profiles.needsConfigurationOnly")}
            />
          }
        >
          {gaps.join(", ")}
        </Alert>
      )}

      {overview && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>{t("profiles.columns.workflowType")}</TableCell>
              <TableCell align="right">{t("profiles.columns.workUnits")}</TableCell>
              <TableCell align="right">{t("profiles.columns.target")}</TableCell>
              <TableCell>{t("profiles.columns.lastChange")}</TableCell>
              <TableCell>{t("profiles.columns.status")}</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row) => {
              const label = workflowTypeLabel(row.workflowType);
              const open = expanded.has(row.workflowType);
              return (
                <Fragment key={row.workflowType}>
                  <TableRow hover>
                    <TableCell padding="checkbox">
                      <IconButton size="small" aria-label={t("profiles.showTestsFor", { name: label })} onClick={() => toggle(row.workflowType)}>
                        {open ? <KeyboardArrowDownIcon /> : <KeyboardArrowRightIcon />}
                      </IconButton>
                    </TableCell>
                    <TableCell>{label}</TableCell>
                    <TableCell align="right">{value(row.effectiveWorkUnits)}</TableCell>
                    <TableCell align="right">{value(row.effectiveTargetAnalystDays)}</TableCell>
                    <TableCell>
                      {row.lastEffectiveFrom ? (
                        <>
                          <Typography variant="body2">{row.lastEffectiveFrom}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {row.lastChangeByName}
                          </Typography>
                        </>
                      ) : (
                        "-"
                      )}
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5}>
                        <Chip size="small" label={t(COVERAGE_LABEL[row.coverage])} color={COVERAGE_COLOR[row.coverage]} />
                        {row.draftCount > 0 && (
                          <Chip size="small" variant="outlined" label={t("profiles.draftCount", { count: row.draftCount })} />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell align="right">
                      <Button size="small" onClick={() => setHistory({ workflowType: row.workflowType, testCode: null })}>
                        {t("profiles.history")}
                      </Button>
                      {canConfigure && (
                        <Button size="small" onClick={() => setAdding({ workflowType: row.workflowType, testCode: null })}>
                          {t("profiles.addVersion")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell colSpan={7} sx={{ py: 0, borderBottom: open ? undefined : "none" }}>
                      <Collapse in={open} unmountOnExit>
                        <Table size="small" sx={{ my: 1 }}>
                          <TableHead>
                            <TableRow>
                              <TableCell>{t("profiles.columns.test")}</TableCell>
                              <TableCell>{t("profiles.columns.name")}</TableCell>
                              <TableCell align="right">{t("profiles.columns.workUnits")}</TableCell>
                              <TableCell align="right">{t("profiles.columns.target")}</TableCell>
                              <TableCell>{t("profiles.columns.status")}</TableCell>
                              <TableCell />
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {visibleTests(row).map((test) => (
                              <TableRow key={test.testCode}>
                                <TableCell>{test.testCode}</TableCell>
                                <TableCell>{test.testName}</TableCell>
                                <TableCell align="right">{value(test.effectiveWorkUnits)}</TableCell>
                                <TableCell align="right">{value(test.effectiveTargetAnalystDays)}</TableCell>
                                <TableCell>
                                  <Stack direction="row" spacing={0.5}>
                                    <Chip size="small" label={t(COVERAGE_LABEL[test.coverage])} color={COVERAGE_COLOR[test.coverage]} />
                                    {test.hasOverride && <Chip size="small" variant="outlined" label={t("profiles.override")} />}
                                  </Stack>
                                </TableCell>
                                <TableCell align="right">
                                  <Button
                                    size="small"
                                    onClick={() => setHistory({ workflowType: row.workflowType, testCode: test.testCode })}
                                  >
                                    {t("profiles.history")}
                                  </Button>
                                  {canConfigure && (
                                    <Button
                                      size="small"
                                      onClick={() => setAdding({ workflowType: row.workflowType, testCode: test.testCode })}
                                    >
                                      {test.hasOverride ? t("profiles.addVersion") : t("profiles.addOverride")}
                                    </Button>
                                  )}
                                </TableCell>
                              </TableRow>
                            ))}
                            {visibleTests(row).length === 0 && (
                              <TableRow>
                                <TableCell colSpan={6}>
                                  <Typography variant="body2" color="text.secondary">
                                    {t("profiles.noTests")}
                                  </Typography>
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </Collapse>
                    </TableCell>
                  </TableRow>
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      )}

      {adding && (
        <AddProfileVersionDialog
          open
          workflowType={adding.workflowType}
          testCode={adding.testCode}
          onClose={() => setAdding(null)}
          onDone={() => void reload()}
        />
      )}
      {history && (
        <ProfileHistoryDrawer
          open
          workflowType={history.workflowType}
          testCode={history.testCode}
          onClose={() => setHistory(null)}
          onChanged={() => void reload()}
        />
      )}
    </Box>
  );
}
