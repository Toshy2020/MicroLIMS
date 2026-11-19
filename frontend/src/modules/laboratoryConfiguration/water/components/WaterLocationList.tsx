import { useState } from "react";
import { useTranslation } from "../../../../i18n";
import { Box, Button, IconButton, Link, Paper, Stack, Tooltip, Typography } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import AddIcon from "@mui/icons-material/Add";
import { LimitPills, ToneChip, frequencyLabel } from "../../../../components/configHierarchy";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { PointHealth, SamplingConfig, SamplingPoint, countTestsOf } from "../waterConfigTypes";

interface Props {
  points: SamplingPoint[];
  configsByPoint: Record<number, SamplingConfig[]>;
  healthByPoint: Record<number, PointHealth>;
  isCountTest: (code: string) => boolean;
  testName: (code: string) => string;
  onEdit: (point: SamplingPoint) => void;
  onDelete: (point: SamplingPoint) => void;
  onAdd: () => void;
  addLabel: string;
  editableCodes?: Set<string>;
}

const COLS = { xs: "1fr", md: "100px minmax(0, 1.3fr) 110px minmax(0, 1.4fr) minmax(0, 1.2fr) 112px" };

// Sample locations of one water system: one row each, expandable to its
// count-test limits. Problems (no tests / missing limits) are flagged on
// the row with a direct link to fix them.
export function WaterLocationList({ points, configsByPoint, healthByPoint, isCountTest, testName, onEdit, onDelete, onAdd, addLabel, editableCodes }: Props) {
  const { t } = useTranslation("labConfig");
  const { options } = useTestDefinitions();
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const toggle = (id: number) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const labName = (code: string) => options.find((o) => o.code === code)?.section?.name || t("water.list.otherLab");

  return (
    <Stack spacing={1.25}>
      <Box sx={{ display: { xs: "none", md: "grid" }, gridTemplateColumns: COLS.md, gap: 1.5, px: 1.75 }}>
        {[t("water.list.headers.code"), t("water.list.headers.location"), t("water.list.headers.frequency"), t("water.list.headers.tests"), t("water.list.headers.limits"), ""].map((h, hi) => (
          <Typography key={hi} sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary" }}>
            {h}
          </Typography>
        ))}
      </Box>

      {points.map((p) => {
        const health = healthByPoint[p.id];
        const configs = configsByPoint[p.id] ?? [];
        const isOpen = expanded.has(p.id);
        const countCodes = countTestsOf(p, isCountTest);
        const assignedCodes = p.assignedTestCodes ?? [];
        const isObservation = (code: string) => options.find((o) => o.code === code)?.workflowType === "Observation";
        const observationCodes = assignedCodes.filter(isObservation);

        const isLockedCode = (code: string) => editableCodes === undefined || !editableCodes.has(code);
        const hasLockedCodes = (p.assignedTestCodes ?? []).some(isLockedCode);

        return (
          <Paper key={p.id} variant="outlined" sx={{ borderRadius: 1.5, overflow: "hidden", borderColor: isOpen ? "primary.main" : "divider" }}>
            <Box sx={{ display: "grid", gridTemplateColumns: COLS, gap: 1.5, px: 1.75, py: 1.5, alignItems: "center" }}>
              <Typography sx={{ fontFamily: "monospace", fontSize: 14, fontWeight: 600 }}>{p.code}</Typography>
              <Typography sx={{ fontSize: 14, overflowWrap: "anywhere" }}>{p.location || "—"}</Typography>
              <Box>
                {frequencyLabel(p) || p.testingFrequency ? <ToneChip label={frequencyLabel(p) || p.testingFrequency} tone="pending" /> : <Typography sx={{ fontSize: 13, color: "text.secondary" }}>—</Typography>}
                {p.labFrequencies && p.labFrequencies.length > 0 && (
                  <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
                    {p.labFrequencies.map((lf) => `${lf.sectionCode}: ${lf.description || frequencyLabel(lf)}`).join(" · ")}
                  </Typography>
                )}
              </Box>
              <Box>
                {health?.noTests ? (
                  <Stack spacing={0.25}>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, color: "warning.dark" }}>{t("water.list.noTests")}</Typography>
                    <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600, textAlign: "left" }}>
                      {t("water.list.assignTests")}
                    </Link>
                  </Stack>
                ) : (
                  <Stack direction="row" sx={{ flexWrap: "wrap", gap: 0.5 }}>
                    {(p.assignedTestCodes ?? []).map((code) => (
                      <Tooltip key={code} title={testName(code)}>
                        <span>
                          <ToneChip label={code} tone="purple" />
                        </span>
                      </Tooltip>
                    ))}
                  </Stack>
                )}
              </Box>
              <Box>
                {health && health.missingLimitCodes.length > 0 ? (
                  <Stack spacing={0.25}>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, color: "warning.dark" }}>
                      {t("water.list.limitsNotSet", { codes: health.missingLimitCodes.join(", ") })}
                    </Typography>
                    <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600, textAlign: "left" }}>
                      {t("water.list.setLimits")}
                    </Link>
                  </Stack>
                ) : countCodes.length > 0 ? (
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.dark" }}>{t("water.list.setForAll")}</Typography>
                ) : (
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{health?.noTests ? "—" : t("water.list.noCountTests")}</Typography>
                )}
              </Box>
              <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
                <Tooltip title={t("water.list.editLocation")}>
                  <IconButton aria-label={t("water.list.editAria", { code: p.code })} onClick={() => onEdit(p)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title={hasLockedCodes ? t("water.list.cannotDelete") : t("water.list.deleteLocation")}>
                  <span>
                    <IconButton
                      aria-label={t("water.list.deleteAria", { code: p.code })}
                      color="error"
                      disabled={hasLockedCodes}
                      onClick={() => onDelete(p)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
                <Tooltip title={isOpen ? t("water.list.hideLimits") : t("water.list.showLimits")}>
                  <IconButton aria-label={t(isOpen ? "water.list.hideAria" : "water.list.showAria", { code: p.code })} aria-expanded={isOpen} onClick={() => toggle(p.id)}>
                    {isOpen ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                  </IconButton>
                </Tooltip>
              </Stack>
            </Box>

            {isOpen && (
              <Box sx={{ px: 2, py: 1.75, borderTop: "1px solid", borderColor: "divider", bgcolor: "background.default" }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary", mb: 1 }}>
                  {t("water.list.limitsHeading")}
                </Typography>
                {assignedCodes.length === 0 ? (
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                    {t("water.list.noTestsAssigned")}
                  </Typography>
                ) : (
                  <Stack spacing={1}>
                    {assignedCodes.map((code) => {
                      const c = configs.find((x) => x.testCode === code);
                      const isEditable = editableCodes !== undefined && editableCodes.has(code);
                      return (
                        <Stack key={code} direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, p: 1.25, border: "1px solid", borderColor: "divider", borderRadius: 1, bgcolor: "background.paper" }}>
                          <Typography sx={{ fontSize: 14, fontWeight: 600, minWidth: 110 }}>{code}</Typography>
                          <Box sx={{ flexGrow: 1 }}>
                            {c ? <LimitPills {...c} /> : <ToneChip label={t("water.list.limitsNotSetChip")} tone="inconclusive" />}
                          </Box>
                          {isEditable ? (
                            <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600 }}>
                              {c ? t("water.list.editLimits") : t("water.list.setLimits")}
                            </Link>
                          ) : (
                            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                              {labName(code)}
                            </Typography>
                          )}
                        </Stack>
                      );
                    })}
                  </Stack>
                )}
                {observationCodes.length > 0 && (
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 1 }}>
                    {t("water.list.presence", { count: observationCodes.length, codes: observationCodes.join(", ") })}
                  </Typography>
                )}
              </Box>
            )}
          </Paper>
        );
      })}

      <Button
        variant="outlined"
        startIcon={<AddIcon />}
        onClick={onAdd}
        sx={{ borderStyle: "dashed", py: 1.25, textTransform: "none", fontWeight: 600 }}
      >
        {addLabel}
      </Button>
    </Stack>
  );
}
