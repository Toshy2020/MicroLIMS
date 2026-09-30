import { useState } from "react";
import { Box, Button, IconButton, Link, Paper, Stack, Tooltip, Typography } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import AddIcon from "@mui/icons-material/Add";
import { LimitPills, ToneChip } from "../../../../components/configHierarchy";
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
}

const COLS = { xs: "1fr", md: "100px minmax(0, 1.3fr) 110px minmax(0, 1.4fr) minmax(0, 1.2fr) 112px" };

// Sample locations of one water system: one row each, expandable to its
// count-test limits. Problems (no tests / missing limits) are flagged on
// the row with a direct link to fix them.
export function WaterLocationList({ points, configsByPoint, healthByPoint, isCountTest, testName, onEdit, onDelete, onAdd, addLabel }: Props) {
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const toggle = (id: number) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <Stack spacing={1.25}>
      <Box sx={{ display: { xs: "none", md: "grid" }, gridTemplateColumns: COLS.md, gap: 1.5, px: 1.75 }}>
        {["Code", "Location", "Frequency", "Assigned tests", "Limits", ""].map((h) => (
          <Typography key={h} sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary" }}>
            {h}
          </Typography>
        ))}
      </Box>

      {points.map((p) => {
        const health = healthByPoint[p.id];
        const configs = configsByPoint[p.id] ?? [];
        const isOpen = expanded.has(p.id);
        const countCodes = countTestsOf(p, isCountTest);
        const otherCodes = (p.assignedTestCodes ?? []).filter((c) => !isCountTest(c));
        return (
          <Paper key={p.id} variant="outlined" sx={{ borderRadius: 1.5, overflow: "hidden", borderColor: isOpen ? "primary.main" : "divider" }}>
            <Box sx={{ display: "grid", gridTemplateColumns: COLS, gap: 1.5, px: 1.75, py: 1.5, alignItems: "center" }}>
              <Typography sx={{ fontFamily: "monospace", fontSize: 14, fontWeight: 600 }}>{p.code}</Typography>
              <Typography sx={{ fontSize: 14, overflowWrap: "anywhere" }}>{p.location || "—"}</Typography>
              <Box>{p.testingFrequency ? <ToneChip label={p.testingFrequency} tone="pending" /> : <Typography sx={{ fontSize: 13, color: "text.secondary" }}>—</Typography>}</Box>
              <Box>
                {health?.noTests ? (
                  <Stack spacing={0.25}>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, color: "warning.dark" }}>No tests assigned</Typography>
                    <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600, textAlign: "left" }}>
                      Assign tests
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
                      Limits not set: {health.missingLimitCodes.join(", ")}
                    </Typography>
                    <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600, textAlign: "left" }}>
                      Set limits
                    </Link>
                  </Stack>
                ) : countCodes.length > 0 ? (
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.dark" }}>Set for all count tests</Typography>
                ) : (
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{health?.noTests ? "—" : "No count tests"}</Typography>
                )}
              </Box>
              <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
                <Tooltip title="Edit location">
                  <IconButton aria-label={`Edit ${p.code}`} onClick={() => onEdit(p)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Delete location">
                  <IconButton aria-label={`Delete ${p.code}`} color="error" onClick={() => onDelete(p)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title={isOpen ? "Hide limits" : "Show limits"}>
                  <IconButton aria-label={`${isOpen ? "Hide" : "Show"} limits for ${p.code}`} aria-expanded={isOpen} onClick={() => toggle(p.id)}>
                    {isOpen ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
                  </IconButton>
                </Tooltip>
              </Stack>
            </Box>

            {isOpen && (
              <Box sx={{ px: 2, py: 1.75, borderTop: "1px solid", borderColor: "divider", bgcolor: "background.default" }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary", mb: 1 }}>
                  Count-test limits
                </Typography>
                {countCodes.length === 0 ? (
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                    No count tests assigned (e.g. TAMC-Water) - limits apply only to count tests.
                  </Typography>
                ) : (
                  <Stack spacing={1}>
                    {countCodes.map((code) => {
                      const c = configs.find((x) => x.testCode === code);
                      return (
                          <Stack key={code} direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, p: 1.25, border: "1px solid", borderColor: "divider", borderRadius: 1, bgcolor: "background.paper" }}>
                            <Typography sx={{ fontSize: 14, fontWeight: 600, minWidth: 110 }}>{code}</Typography>
                            <Box sx={{ flexGrow: 1 }}>
                              {c ? <LimitPills {...c} /> : <ToneChip label="Limits not set" tone="inconclusive" />}
                            </Box>
                            <Link component="button" type="button" onClick={() => onEdit(p)} sx={{ fontSize: 13, fontWeight: 600 }}>
                              {c ? "Edit limits" : "Set limits"}
                            </Link>
                          </Stack>
                      );
                    })}
                  </Stack>
                )}
                {otherCodes.length > 0 && (
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 1 }}>
                    {otherCodes.join(", ")} {otherCodes.length === 1 ? "is a presence/absence test" : "are presence/absence tests"} - no numeric limits needed.
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
