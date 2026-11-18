import { useMemo, useState } from "react";
import {
  Alert, Button, Chip, LinearProgress, Paper, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TableSortLabel, Typography, useTheme
} from "@mui/material";
import type { AnalystWorkload, AnalystWorkloadRow } from "../types/workloadTypes";
import { formatRatio, formatUnits, sortByName } from "../utils/kpiFormat";
import { useTranslation } from "../../../i18n";

type SortKey =
  | "capacityUnits" | "deliveredUnits" | "utilisation" | "openUnits"
  | "forwardLoad" | "weightedOnTime" | "weightedRightFirstTime";

const COLUMNS: { key: SortKey; label: string }[] = [
  { key: "capacityUnits", label: "workload.columns.capacityUnits" },
  { key: "deliveredUnits", label: "workload.columns.deliveredUnits" },
  { key: "utilisation", label: "workload.columns.utilisation" },
  { key: "openUnits", label: "workload.columns.openUnits" },
  { key: "forwardLoad", label: "workload.columns.forwardLoad" },
  { key: "weightedOnTime", label: "workload.columns.weightedOnTime" },
  { key: "weightedRightFirstTime", label: "workload.columns.weightedRightFirstTime" }
];

type Sort = { key: SortKey; dir: "asc" | "desc" } | null;

// Missing values always sort last, so a blank never looks like the best or worst.
function compareBy(key: SortKey, dir: "asc" | "desc") {
  return (a: AnalystWorkloadRow, b: AnalystWorkloadRow) => {
    const x = a[key];
    const y = b[key];
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    return dir === "asc" ? x - y : y - x;
  };
}

interface WorkloadTableProps {
  data: AnalystWorkload | null;
  loading: boolean;
  error: string | null;
  onOpenTests(userId: number): void;
}

export function WorkloadTable({ data, loading, error, onOpenTests }: WorkloadTableProps) {
  const { t } = useTranslation("kpi");
  const theme = useTheme();
  // Held in state only: an explicit sort is a look at the data, not a saved preference.
  const [sort, setSort] = useState<Sort>(null);

  const rows = useMemo(() => {
    const alphabetical = sortByName(data?.rows ?? []);
    return sort ? [...alphabetical].sort(compareBy(sort.key, sort.dir)) : alphabetical;
  }, [data, sort]);

  const toggle = (key: SortKey) =>
    setSort((prev) => (!prev || prev.key !== key ? { key, dir: "asc" } : prev.dir === "asc" ? { key, dir: "desc" } : null));

  if (!data && !loading && !error) return null;

  const amber = theme.custom.status.inconclusive;
  const info = theme.custom.status.info;
  const chip = (label: string, tone: { bg: string; text: string; border: string }, ml = 0) => (
    <Chip key={label} size="small" label={label} sx={{ ml, bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}`, fontSize: 12 }} />
  );

  const cell = (r: AnalystWorkloadRow, key: SortKey) => {
    switch (key) {
      case "capacityUnits": return formatUnits(r.capacityUnits);
      case "openUnits": return formatUnits(r.openUnits);
      case "deliveredUnits":
        return (
          <>
            {formatUnits(r.deliveredUnits)}
            <Typography component="span" sx={{ display: "block", fontSize: 12, color: "text.secondary" }}>
              {t("workload.testsCount", { count: r.deliveredTests })}
            </Typography>
          </>
        );
      case "utilisation":
        return (
          <>
            {formatRatio(r.utilisation)}
            {r.utilisationCheckProfile && chip(t("workload.checkProfile"), info, 0.5)}
          </>
        );
      case "forwardLoad":
        return (
          <>
            {formatRatio(r.forwardLoad)}
            {r.forwardLoadOverCapacity && chip(t("workload.overCapacity"), amber, 0.5)}
          </>
        );
      case "weightedOnTime": return formatRatio(r.weightedOnTime);
      case "weightedRightFirstTime": return formatRatio(r.weightedRightFirstTime);
    }
  };

  return (
    <Paper component="section" aria-label={t("workload.title")} sx={{ p: 2, mb: 3 }}>
      <Typography component="h2" sx={{ fontSize: 16, fontWeight: 700 }}>{t("workload.title")}</Typography>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1.5 }}>
        {t("workload.intro")}
      </Typography>

      {loading && <LinearProgress sx={{ mb: 1 }} />}
      {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}

      {data && (
        <Stack spacing={1}>
          {data.onlyOwnRow && <Alert severity="info">{t("workload.ownOnly")}</Alert>}
          {data.capacityUsesDefaults && (
            <Alert severity="info">{t("workload.capacityDefaults")}</Alert>
          )}
          {(data.unassignedOpenTests > 0 || data.gaps.length > 0) && (
            <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
              {data.unassignedOpenTests > 0 && chip(t("workload.unassigned", { count: data.unassignedOpenTests }), amber)}
              {data.gaps.map((g) => chip(g.message, amber))}
            </Stack>
          )}

          {rows.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("workload.noAnalysts")}</Typography>
          ) : (
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t("workload.analyst")}</TableCell>
                    {COLUMNS.map((c) => (
                      <TableCell key={c.key} align="right" sortDirection={sort?.key === c.key ? sort.dir : false}>
                        <TableSortLabel
                          active={sort?.key === c.key}
                          direction={sort?.key === c.key ? sort.dir : "asc"}
                          onClick={() => toggle(c.key)}
                        >
                          {t(c.label)}
                        </TableSortLabel>
                      </TableCell>
                    ))}
                    <TableCell>{t("workload.gaps")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.userId}>
                      <TableCell>
                        <Button
                          size="small"
                          onClick={() => onOpenTests(r.userId)}
                          aria-label={t("workload.showTestsFor", { name: r.fullName })}
                          sx={{ textTransform: "none", fontWeight: 600 }}
                        >
                          {r.fullName}
                        </Button>
                      </TableCell>
                      {COLUMNS.map((c) => <TableCell key={c.key} align="right">{cell(r, c.key)}</TableCell>)}
                      <TableCell>
                        <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
                          {r.unweightedTests > 0 && chip(t("workload.unweightedDelivered", { count: r.unweightedTests }), amber)}
                          {r.unweightedOpenTests > 0 && chip(t("workload.unweightedOpen", { count: r.unweightedOpenTests }), amber)}
                          {r.testsOnDefaultTarget > 0 && chip(t("workload.onDefaultTarget", { count: r.testsOnDefaultTarget }), amber)}
                        </Stack>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Stack>
      )}
    </Paper>
  );
}
