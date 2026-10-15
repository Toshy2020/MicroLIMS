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

interface Scope {
  workflowType: LiveWorkflowType;
  testCode: string | null;
}

const COVERAGE_LABEL: Record<ProfileCoverage, string> = {
  Configured: "Configured",
  Partial: "Partial",
  NeedsConfiguration: "Needs configuration"
};
const COVERAGE_COLOR: Record<ProfileCoverage, "success" | "warning" | "default"> = {
  Configured: "success",
  Partial: "warning",
  NeedsConfiguration: "default"
};

const value = (n: number | null) => (n === null ? "Not set" : String(n));
const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

// Configuration only: effective work units and targets per workflow type, with per-test
// overrides underneath. It never shows analyst figures (no ranking, spec D6).
export function WorkflowProfilesPanel() {
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
    onlyGaps ? row.tests.filter((t) => needsWork(t.coverage)) : row.tests;
  const rows = (overview?.types ?? []).filter(
    (t) => !onlyGaps || needsWork(t.coverage) || t.tests.some((x) => needsWork(x.coverage))
  );

  const gaps = coverage
    ? [
        `${plural(coverage.typesWithoutUnits.length, "workflow type", "workflow types")} without work units`,
        `${coverage.typesWithoutTarget.length} without a target`,
        `${plural(coverage.testsNeedingConfiguration.length, "test needs", "tests need")} configuration`
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
        Work units: 1 unit = 1 standard analyst-hour of hands-on work. Target: analyst-stage days. A version takes effect on
        its effective date and is never edited; correct a mistake by adding a new version. Tests without a profile stay
        unweighted - they are never counted as 1.0.
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
              label="Needs configuration only"
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
              <TableCell>Workflow type</TableCell>
              <TableCell align="right">Work units</TableCell>
              <TableCell align="right">Target (days)</TableCell>
              <TableCell>Last change</TableCell>
              <TableCell>Status</TableCell>
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
                      <IconButton size="small" aria-label={`Show tests for ${label}`} onClick={() => toggle(row.workflowType)}>
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
                        <Chip size="small" label={COVERAGE_LABEL[row.coverage]} color={COVERAGE_COLOR[row.coverage]} />
                        {row.draftCount > 0 && (
                          <Chip size="small" variant="outlined" label={plural(row.draftCount, "draft", "drafts")} />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell align="right">
                      <Button size="small" onClick={() => setHistory({ workflowType: row.workflowType, testCode: null })}>
                        History
                      </Button>
                      {canConfigure && (
                        <Button size="small" onClick={() => setAdding({ workflowType: row.workflowType, testCode: null })}>
                          Add version
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
                              <TableCell>Test</TableCell>
                              <TableCell>Name</TableCell>
                              <TableCell align="right">Work units</TableCell>
                              <TableCell align="right">Target (days)</TableCell>
                              <TableCell>Status</TableCell>
                              <TableCell />
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {visibleTests(row).map((t) => (
                              <TableRow key={t.testCode}>
                                <TableCell>{t.testCode}</TableCell>
                                <TableCell>{t.testName}</TableCell>
                                <TableCell align="right">{value(t.effectiveWorkUnits)}</TableCell>
                                <TableCell align="right">{value(t.effectiveTargetAnalystDays)}</TableCell>
                                <TableCell>
                                  <Stack direction="row" spacing={0.5}>
                                    <Chip size="small" label={COVERAGE_LABEL[t.coverage]} color={COVERAGE_COLOR[t.coverage]} />
                                    {t.hasOverride && <Chip size="small" variant="outlined" label="Override" />}
                                  </Stack>
                                </TableCell>
                                <TableCell align="right">
                                  <Button
                                    size="small"
                                    onClick={() => setHistory({ workflowType: row.workflowType, testCode: t.testCode })}
                                  >
                                    History
                                  </Button>
                                  {canConfigure && (
                                    <Button
                                      size="small"
                                      onClick={() => setAdding({ workflowType: row.workflowType, testCode: t.testCode })}
                                    >
                                      {t.hasOverride ? "Add version" : "Add override"}
                                    </Button>
                                  )}
                                </TableCell>
                              </TableRow>
                            ))}
                            {visibleTests(row).length === 0 && (
                              <TableRow>
                                <TableCell colSpan={6}>
                                  <Typography variant="body2" color="text.secondary">
                                    No tests to show for this workflow type.
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
