import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, Grid, LinearProgress, Typography } from "@mui/material";
import { useAuth, useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { useKpiDashboard } from "../hooks/useKpiDashboard";
import { useKpiWorkload } from "../hooks/useKpiWorkload";
import type { KpiDefinition, KpiEntry, KpiRecordPage } from "../types/kpiTypes";
import { exportKpiPdf } from "../utils/exportKpiPdf";
import { defaultFilters, toQueryParams } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { DefinitionsDrawer } from "./DefinitionsDrawer";
import { KpiFilters } from "./KpiFilters";
import { KpiRecordsDrawer } from "./KpiRecordsDrawer";
import { KpiSection } from "./KpiSection";
import { KpiTile } from "./KpiTile";
import { WorkflowProfilesDialog } from "./WorkflowProfilesDialog";
import { WorkloadTable } from "./WorkloadTable";

// The sections regroup the registry by topic (spec section 5); the backend's
// Group says "Headline" for the eight tiles, several of which also trend here.
const FLOW_KEYS = ["stage-tat", "backlog-aging"];
const QUALITY_KEYS = ["right-first-time", "retest-rate", "oos-rate", "returns-by-test"];
const MICRO_KEYS = ["em-excursion-rate", "incubation-reads-on-time"];

const pick = (entries: KpiEntry[], keys: string[]) =>
  keys.map((k) => entries.find((e) => e.key === k)).filter((e): e is KpiEntry => e !== undefined);

interface RecordsTarget {
  title: string;
  load: (page: number, pageSize: number) => Promise<KpiRecordPage>;
}

export function KpiPerformanceTab() {
  const { fullName } = useAuth();
  const canConfigureProfiles = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [filters, setFilters] = useState<KpiFiltersValue>(() => defaultFilters());
  const dash = useKpiDashboard(filters);
  const workload = useKpiWorkload(filters);

  const [definitionsOpen, setDefinitionsOpen] = useState(false);
  const [profilesOpen, setProfilesOpen] = useState(false);
  const [records, setRecords] = useState<RecordsTarget | null>(null);
  const [definitions, setDefinitions] = useState<Map<string, KpiDefinition>>(new Map());

  // Hover text for the tiles. The hover is a convenience: if this call fails the
  // tiles simply have no tooltip, and the Definitions drawer reports its own failure.
  useEffect(() => {
    let live = true;
    kpiDashboardService.definitions(filters.from, filters.to)
      .then((d) => { if (live) setDefinitions(new Map(d.kpis.map((k) => [k.key, k]))); })
      .catch(() => { if (live) setDefinitions(new Map()); });
    return () => { live = false; };
  }, [filters.from, filters.to]);

  // Memoised on dash.data so the empty fallback is not a new array every render (react-hooks/exhaustive-deps).
  const entries = useMemo(() => dash.data?.kpis ?? [], [dash.data]);
  const headline = useMemo(
    () => entries.filter((k) => k.group === "Headline").sort((a, b) => a.order - b.order),
    [entries]
  );

  const openRecords = (key: string) => {
    const name = entries.find((k) => k.key === key)?.name ?? key;
    setRecords({
      title: `${name} - records`,
      load: (page, pageSize) => kpiDashboardService.records(key, { ...toQueryParams(filters), page, pageSize })
    });
  };

  const openTests = (userId: number) => {
    const name = workload.data?.rows.find((r) => r.userId === userId)?.fullName ?? "Analyst";
    setRecords({
      title: `${name} - tests`,
      load: (page, pageSize) => kpiWorkloadService.workloadTests({ ...toQueryParams(filters), analystId: userId, page, pageSize })
    });
  };

  const exportDisabled = !dash.data || dash.loading || workload.loading;

  return (
    <Box sx={{ pb: 4 }}>
      <KpiFilters
        value={filters}
        onChange={setFilters}
        onOpenDefinitions={() => setDefinitionsOpen(true)}
        onOpenProfiles={canConfigureProfiles ? () => setProfilesOpen(true) : undefined}
        onExport={() => { if (dash.data) exportKpiPdf(dash.data, workload.data, filters, fullName || "Authorized User", workload.error); }}
        exportDisabled={exportDisabled}
      />

      {dash.loading && <LinearProgress sx={{ mb: 1 }} />}
      {dash.error && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={() => void dash.reload()}>Retry</Button>}>
          {dash.error}
        </Alert>
      )}

      {dash.data && (
        <Box sx={{ opacity: dash.loading ? 0.6 : 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1.5 }}>
            Calculated {new Date(dash.data.generatedAt).toLocaleString()} for your laboratory sections. Click any figure to see the records behind it.
          </Typography>

          <Grid container spacing={2} sx={{ mb: 3 }}>
            {headline.map((e) => (
              <Grid key={e.key} size={{ xs: 12, sm: 6, md: 3 }}>
                <KpiTile entry={e} definition={definitions.get(e.key)} onOpenRecords={openRecords} />
              </Grid>
            ))}
          </Grid>

          <KpiSection title="Flow" entries={pick(entries, FLOW_KEYS)} onOpenRecords={openRecords} />
          <KpiSection title="Quality" entries={pick(entries, QUALITY_KEYS)} onOpenRecords={openRecords} />
          <KpiSection title="Micro" entries={pick(entries, MICRO_KEYS)} onOpenRecords={openRecords} />
        </Box>
      )}

      {/* The workload block has its own request and its own error: a refusal here must not blank the tiles above. */}
      <WorkloadTable data={workload.data} loading={workload.loading} error={workload.error} onOpenTests={openTests} />

      <DefinitionsDrawer open={definitionsOpen} onClose={() => setDefinitionsOpen(false)} from={filters.from} to={filters.to} />
      <WorkflowProfilesDialog open={profilesOpen} onClose={() => setProfilesOpen(false)} />
      {records && <KpiRecordsDrawer title={records.title} onClose={() => setRecords(null)} load={records.load} />}
    </Box>
  );
}
