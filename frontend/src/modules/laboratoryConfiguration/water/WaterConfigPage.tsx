import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ConfigMasterList, MasterListItem, SummaryTiles } from "../../../components/configHierarchy";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { WaterConfigService } from "./services/WaterConfigService";
import { WaterLocationList } from "./components/WaterLocationList";
import { WaterDepartmentPanel } from "./components/WaterDepartmentPanel";
import { WaterLocationPanel } from "./components/WaterLocationPanel";
import { PointHealth, SamplingConfig, SamplingPoint, WaterDept, needsAttention, plural, pointHealth } from "./waterConfigTypes";

type Filter = "all" | "attention";

const NO_CONFIGS: SamplingConfig[] = [];

// Water systems -> sample locations -> assigned tests and per-count-test
// limits, read by WaterWorkflowEngine on every water sample receipt
// (assigned tests) and calculation (limits). Master/detail: systems on
// the left, the selected system's locations on the right, every add/edit
// in a side panel.
export function WaterConfigPage() {
  const { options } = useTestDefinitions();
  const [departments, setDepartments] = useState<WaterDept[]>([]);
  const [configsByPoint, setConfigsByPoint] = useState<Record<number, SamplingConfig[]>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  const [deptPanel, setDeptPanel] = useState<{ open: boolean; dept: WaterDept | null }>({ open: false, dept: null });
  const [pointPanel, setPointPanel] = useState<{ open: boolean; point: SamplingPoint | null }>({ open: false, point: null });
  const [pendingDeleteDept, setPendingDeleteDept] = useState<WaterDept | null>(null);
  const [pendingDeletePoint, setPendingDeletePoint] = useState<SamplingPoint | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const depts: WaterDept[] = await WaterConfigService.getWaterDepartments();
      const points = depts.flatMap((d) => d.samplingPoints ?? []);
      // No bulk endpoint - one request per location, in parallel.
      const configs = await Promise.all(
        points.map((p) => WaterConfigService.getSamplingConfigurations(p.id).catch(() => [] as SamplingConfig[]))
      );
      setDepartments(depts);
      setConfigsByPoint(Object.fromEntries(points.map((p, i) => [p.id, configs[i] ?? []])));
      setLoadError(null);
      setSelectedDeptId((current) => (current != null && depts.some((d) => d.id === current) ? current : depts[0]?.id ?? null));
    } catch (e: any) {
      setLoadError(e?.response?.data?.message ?? "Could not load the water configuration.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const isCountTest = useCallback(
    (code: string) => options.find((o) => o.code === code)?.workflowType === "CountTest",
    [options]
  );
  const testName = useCallback((code: string) => options.find((o) => o.code === code)?.displayName || code, [options]);

  const allPoints = useMemo(() => departments.flatMap((d) => d.samplingPoints ?? []), [departments]);
  const healthByPoint = useMemo(
    () => Object.fromEntries(allPoints.map((p) => [p.id, pointHealth(p, configsByPoint[p.id] ?? [], isCountTest)])) as Record<number, PointHealth>,
    [allPoints, configsByPoint, isCountTest]
  );

  const q = search.trim().toLowerCase();
  const matchesPoint = (p: SamplingPoint) => !q || p.code.toLowerCase().includes(q) || (p.location ?? "").toLowerCase().includes(q);

  const listItems: MasterListItem[] = departments
    .filter((d) => !q || d.name.toLowerCase().includes(q) || (d.samplingPoints ?? []).some(matchesPoint))
    .map((d) => {
      const pts = d.samplingPoints ?? [];
      const attention = pts.filter((p) => healthByPoint[p.id] && needsAttention(healthByPoint[p.id])).length;
      return {
        id: d.id,
        title: d.name,
        subtitle: pts.length === 0 ? "No locations yet" : plural(pts.length, "location"),
        badge: pts.length === 0 ? undefined : attention > 0 ? { label: `${attention} need${attention === 1 ? "s" : ""} attention`, tone: "inconclusive" } : { label: "Complete", tone: "notDetected" }
      };
    });

  // Memoized: the panel resets its form whenever this list changes.
  const pointPanelConfigs = useMemo(
    () => (pointPanel.point ? configsByPoint[pointPanel.point.id] ?? NO_CONFIGS : NO_CONFIGS),
    [pointPanel.point, configsByPoint]
  );

  const selectedDept = departments.find((d) => d.id === selectedDeptId) ?? null;
  const deptPoints = (selectedDept?.samplingPoints ?? []).filter(matchesPoint);
  const attentionPoints = deptPoints.filter((p) => healthByPoint[p.id] && needsAttention(healthByPoint[p.id]));
  const shownPoints = filter === "attention" ? attentionPoints : deptPoints;

  const missingLimitsCount = allPoints.filter((p) => (healthByPoint[p.id]?.missingLimitCodes.length ?? 0) > 0).length;
  const noTestsCount = allPoints.filter((p) => healthByPoint[p.id]?.noTests).length;

  const afterSave = (text: string, selectId?: number) => {
    setDeptPanel({ open: false, dept: null });
    setPointPanel({ open: false, point: null });
    setMessage({ text, ok: true });
    if (selectId != null) setSelectedDeptId(selectId);
    load();
  };

  const deleteDept = async (d: WaterDept) => {
    setPendingDeleteDept(null);
    setMessage(null);
    try {
      await WaterConfigService.deleteWaterDepartment(d.id);
      setMessage({ text: `"${d.name}" deleted.`, ok: true });
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this water system.", ok: false });
    }
  };

  const deletePoint = async (p: SamplingPoint) => {
    setPendingDeletePoint(null);
    setMessage(null);
    try {
      await WaterConfigService.deleteSamplingPoint(p.id);
      setMessage({ text: `Sample location ${p.code} deleted.`, ok: true });
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this sample location.", ok: false });
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title="Water" subtitle="Water systems, their sample locations, assigned tests and per-location limits." />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            Add water system
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            disabled={departments.length === 0}
            onClick={() => setPointPanel({ open: true, point: null })}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            Add sample location
          </Button>
        </Stack>
      </Stack>

      {loadError && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={load}>Retry</Button>}>
          {loadError}
        </Alert>
      )}
      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <SummaryTiles
        tiles={[
          { label: "Water systems", value: departments.length },
          { label: "Sample locations", value: allPoints.length },
          { label: "Missing limits", value: plural(missingLimitsCount, "location"), tone: missingLimitsCount > 0 ? "inconclusive" : undefined },
          { label: "No tests assigned", value: plural(noTestsCount, "location"), tone: noTestsCount > 0 ? "inconclusive" : undefined }
        ]}
      />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "300px minmax(0, 1fr)" }, gap: 2.5, alignItems: "start" }}>
        <ConfigMasterList
          searchLabel="Find a system or location"
          searchPlaceholder="e.g. PW-02 or WFI"
          search={search}
          onSearchChange={setSearch}
          items={listItems}
          selectedId={selectedDeptId}
          onSelect={(id) => {
            setSelectedDeptId(id);
            setFilter("all");
          }}
          loading={loading}
          emptyText={q ? "Nothing matches your search." : "No water systems yet. Use “Add water system” to create one."}
        />

        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
          {!selectedDept ? (
            <Box sx={{ p: 4, textAlign: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{loading ? "Loading..." : "Select a water system"}</Typography>
            </Box>
          ) : (
            <>
              <Stack direction="row" sx={{ px: 2.75, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700 }}>{selectedDept.name}</Typography>
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                    {plural((selectedDept.samplingPoints ?? []).length, "sample location")}
                  </Typography>
                </Box>
                <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: selectedDept })}>
                  Rename
                </Button>
                <Tooltip title="Delete water system">
                  <IconButton aria-label={`Delete ${selectedDept.name}`} color="error" onClick={() => setPendingDeleteDept(selectedDept)}>
                    <DeleteIcon />
                  </IconButton>
                </Tooltip>
              </Stack>

              <Box sx={{ px: 2.75, pt: 1.75, pb: 2.75 }}>
                {deptPoints.length > 0 && (
                  <Stack direction="row" spacing={1} sx={{ mb: 1.75 }}>
                    <Button
                      size="small"
                      variant={filter === "all" ? "contained" : "outlined"}
                      aria-pressed={filter === "all"}
                      onClick={() => setFilter("all")}
                      sx={{ borderRadius: 999, textTransform: "none" }}
                    >
                      All ({deptPoints.length})
                    </Button>
                    <Button
                      size="small"
                      variant={filter === "attention" ? "contained" : "outlined"}
                      aria-pressed={filter === "attention"}
                      onClick={() => setFilter("attention")}
                      sx={{ borderRadius: 999, textTransform: "none" }}
                    >
                      Needs attention ({attentionPoints.length})
                    </Button>
                  </Stack>
                )}

                {shownPoints.length === 0 && (
                  <Typography sx={{ fontSize: 14, color: "text.secondary", mb: 1.5 }}>
                    {filter === "attention"
                      ? "Every location here has its tests and limits set."
                      : q
                        ? "No locations here match your search."
                        : "No sample locations yet."}
                  </Typography>
                )}

                <WaterLocationList
                  points={shownPoints}
                  configsByPoint={configsByPoint}
                  healthByPoint={healthByPoint}
                  isCountTest={isCountTest}
                  testName={testName}
                  onEdit={(p) => setPointPanel({ open: true, point: p })}
                  onDelete={setPendingDeletePoint}
                  onAdd={() => setPointPanel({ open: true, point: null })}
                  addLabel={`Add sample location to ${selectedDept.name}`}
                />
              </Box>
            </>
          )}
        </Paper>
      </Box>

      <WaterDepartmentPanel
        open={deptPanel.open}
        dept={deptPanel.dept}
        onClose={() => setDeptPanel({ open: false, dept: null })}
        onSaved={afterSave}
      />
      <WaterLocationPanel
        open={pointPanel.open}
        point={pointPanel.point}
        defaultDepartmentId={selectedDeptId}
        departments={departments}
        configs={pointPanelConfigs}
        isCountTest={isCountTest}
        onClose={() => {
          setPointPanel({ open: false, point: null });
          // A partly failed save may have written the location already.
          load();
        }}
        onSaved={(text) => afterSave(text)}
      />

      <ConfirmationDialog
        destructive
        open={pendingDeleteDept != null}
        message={pendingDeleteDept ? `Delete water system "${pendingDeleteDept.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeleteDept(null)}
        onConfirm={() => pendingDeleteDept && deleteDept(pendingDeleteDept)}
      />
      <ConfirmationDialog
        destructive
        open={pendingDeletePoint != null}
        message={pendingDeletePoint ? `Delete sample location "${pendingDeletePoint.code}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDeletePoint(null)}
        onConfirm={() => pendingDeletePoint && deletePoint(pendingDeletePoint)}
      />
    </Box>
  );
}
