import { useCallback, useEffect, useMemo, useState } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
import { Alert, Box, Button, IconButton, Paper, Stack, TextField, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import DeleteIcon from "@mui/icons-material/Delete";
import { i18n, useTranslation } from "../../../i18n";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ConfigMasterList, MasterListItem, SummaryTiles } from "../../../components/configHierarchy";
import { parseClipboardTable } from "../../../components/entry/parseClipboardTable";
import { PastePreviewDialog } from "../../../components/entry/PastePreviewDialog";
import type { PasteAcceptedRow } from "../../../components/entry/PastePreviewDialog";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { useEditableTestCodes } from "../../../hooks/useEditableTestCodes";
import { getErrorMessage } from "../../../utils/errorMessage";
import { extractBatchSaveErrors } from "../../../utils/fieldErrors";
import { WaterConfigService } from "./services/WaterConfigService";
import { WaterLocationList } from "./components/WaterLocationList";
import { WaterLocationPendingList } from "./components/WaterLocationPendingList";
import { WaterDepartmentPanel } from "./components/WaterDepartmentPanel";
import { WaterLocationPanel } from "./components/WaterLocationPanel";
import { PointHealth, SamplingConfig, SamplingPoint, WaterDept, needsAttention, pointHealth } from "./waterConfigTypes";
import {
  buildWaterSamplingPointPastePlan,
  toCreateWaterSamplingPointPayload,
  toPendingWaterPoints
} from "./waterSamplingPointPaste";
import type { PendingWaterPoint } from "./waterSamplingPointPaste";

type Filter = "all" | "attention";

const NO_CONFIGS: SamplingConfig[] = [];

// Water systems -> sample locations -> assigned tests and per-count-test
// limits, read by WaterWorkflowEngine on every water sample receipt
// (assigned tests) and calculation (limits). Master/detail: systems on
// the left, the selected system's locations on the right, every add/edit
// in a side panel.
export function WaterConfigPage() {
  const { t } = useTranslation("labConfig");
  const { options } = useTestDefinitions();
  const { editableCodes } = useEditableTestCodes();
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
  const [pasteBarOpen, setPasteBarOpen] = useState(false);
  const [pasteCells, setPasteCells] = useState<string[][] | null>(null);
  const [pendingPoints, setPendingPoints] = useState<PendingWaterPoint[]>([]);
  const [savingBatch, setSavingBatch] = useState(false);
  const [batchError, setBatchError] = useState<string | null>(null);

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
    } catch (e: unknown) {
      setLoadError(getErrorMessage(e, i18n.t("water.page.loadError", { ns: "labConfig" })));
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
  const pastePlan = useMemo(
    () => buildWaterSamplingPointPastePlan({ departments, existingPoints: allPoints, testOptions: options }),
    [departments, allPoints, options]
  );

  const anyModalOpen =
    deptPanel.open ||
    pointPanel.open ||
    pendingDeleteDept !== null ||
    pendingDeletePoint !== null ||
    pasteCells !== null;

  useEffect(() => {
    if (anyModalOpen || departments.length === 0) return;
    const handleDocumentPaste = (e: ClipboardEvent) => {
      if (e.defaultPrevented) return;
      const target = e.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }
      const text = e.clipboardData?.getData("text/plain") ?? "";
      const table = parseClipboardTable(text);
      if (table.length === 0) return;
      e.preventDefault();
      setPasteCells(table);
    };
    document.addEventListener("paste", handleDocumentPaste);
    return () => document.removeEventListener("paste", handleDocumentPaste);
  }, [anyModalOpen, departments.length]);

  const handlePasteInput = (e: ReactClipboardEvent<HTMLElement>) => {
    const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
    if (table.length === 0) return;
    e.preventDefault();
    e.stopPropagation();
    setPasteCells(table);
  };

  const handleAcceptPaste = (rows: PasteAcceptedRow[]) => {
    setPendingPoints(toPendingWaterPoints(rows, departments, options));
    setBatchError(null);
    setPasteCells(null);
    setPasteBarOpen(false);
  };

  const handleRemovePendingRow = (index: number) => {
    setPendingPoints((prev) => prev.filter((_, i) => i !== index));
    setBatchError(null);
  };

  const saveBatch = async () => {
    if (pendingPoints.length === 0) return;
    setSavingBatch(true);
    setBatchError(null);
    try {
      const payload = pendingPoints.map(toCreateWaterSamplingPointPayload);
      await WaterConfigService.createSamplingPointsBatch(payload);
      const count = pendingPoints.length;
      setPendingPoints([]);
      setMessage({ text: t("water.page.addedLocations", { locations: t("water.sampleLocationCount", { count }) }), ok: true });
      await load();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        t("water.page.batchError")
      );
      if (rowErrors) {
        setPendingPoints((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setBatchError(summary);
    } finally {
      setSavingBatch(false);
    }
  };
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
        subtitle: pts.length === 0 ? t("water.page.noLocationsYet") : t("water.locationCount", { count: pts.length }),
        badge: pts.length === 0 ? undefined : attention > 0 ? { label: t("water.page.needAttention", { count: attention }), tone: "inconclusive" } : { label: t("water.page.complete"), tone: "notDetected" }
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
      setMessage({ text: t("water.page.deleted", { name: d.name }), ok: true });
      load();
    } catch (e: unknown) {
      setMessage({ text: getErrorMessage(e, t("water.page.deleteDeptError")), ok: false });
    }
  };

  const deletePoint = async (p: SamplingPoint) => {
    setPendingDeletePoint(null);
    setMessage(null);
    try {
      await WaterConfigService.deleteSamplingPoint(p.id);
      setMessage({ text: t("water.page.pointDeleted", { code: p.code }), ok: true });
      load();
    } catch (e: unknown) {
      setMessage({ text: getErrorMessage(e, t("water.page.deletePointError")), ok: false });
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title={t("water.page.title")} subtitle={t("water.page.subtitle")} />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            {t("water.page.addSystem")}
          </Button>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            disabled={departments.length === 0}
            aria-expanded={pasteBarOpen}
            onClick={() => setPasteBarOpen((v) => !v)}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            {t("water.page.pasteLocations")}
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            disabled={departments.length === 0}
            onClick={() => setPointPanel({ open: true, point: null })}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            {t("water.page.addLocation")}
          </Button>
        </Stack>
      </Stack>

      {pasteBarOpen && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2, borderRadius: 2 }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
            <TextField
              autoFocus
              size="small"
              fullWidth
              label={t("water.page.pasteLabel")}
              placeholder={t("water.page.pastePlaceholder")}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={t("water.page.pasteHelper")}
            />
            <Button onClick={() => setPasteBarOpen(false)} sx={{ textTransform: "none", flexShrink: 0 }}>
              {t("water.page.close")}
            </Button>
          </Stack>
        </Paper>
      )}

      {loadError && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={load}>{t("water.page.retry")}</Button>}>
          {loadError}
        </Alert>
      )}
      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <WaterLocationPendingList
        rows={pendingPoints}
        saving={savingBatch}
        error={batchError}
        onSave={saveBatch}
        onDiscard={() => {
          setPendingPoints([]);
          setBatchError(null);
        }}
        onRemoveRow={handleRemovePendingRow}
      />

      <SummaryTiles
        tiles={[
          { label: t("water.page.tiles.systems"), value: departments.length },
          { label: t("water.page.tiles.locations"), value: allPoints.length },
          { label: t("water.page.tiles.missingLimits"), value: t("water.locationCount", { count: missingLimitsCount }), tone: missingLimitsCount > 0 ? "inconclusive" : undefined },
          { label: t("water.page.tiles.noTests"), value: t("water.locationCount", { count: noTestsCount }), tone: noTestsCount > 0 ? "inconclusive" : undefined }
        ]}
      />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "300px minmax(0, 1fr)" }, gap: 2.5, alignItems: "start" }}>
        <ConfigMasterList
          searchLabel={t("water.page.searchLabel")}
          searchPlaceholder={t("water.page.searchPlaceholder")}
          search={search}
          onSearchChange={setSearch}
          items={listItems}
          selectedId={selectedDeptId}
          onSelect={(id) => {
            setSelectedDeptId(id);
            setFilter("all");
          }}
          loading={loading}
          emptyText={q ? t("water.page.noMatch") : t("water.page.noSystems")}
        />

        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
          {!selectedDept ? (
            <Box sx={{ p: 4, textAlign: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{loading ? t("water.page.loading") : t("water.page.selectSystem")}</Typography>
            </Box>
          ) : (
            <>
              <Stack direction="row" sx={{ px: 2.75, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700 }}>{selectedDept.name}</Typography>
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                    {t("water.sampleLocationCount", { count: (selectedDept.samplingPoints ?? []).length })}
                  </Typography>
                </Box>
                <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: selectedDept })}>
                  {t("water.page.rename")}
                </Button>
                <Tooltip title={t("water.page.deleteSystem")}>
                  <IconButton aria-label={t("water.page.deleteAria", { name: selectedDept.name })} color="error" onClick={() => setPendingDeleteDept(selectedDept)}>
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
                      {t("water.page.filterAll", { count: deptPoints.length })}
                    </Button>
                    <Button
                      size="small"
                      variant={filter === "attention" ? "contained" : "outlined"}
                      aria-pressed={filter === "attention"}
                      onClick={() => setFilter("attention")}
                      sx={{ borderRadius: 999, textTransform: "none" }}
                    >
                      {t("water.page.filterAttention", { count: attentionPoints.length })}
                    </Button>
                  </Stack>
                )}

                {shownPoints.length === 0 && (
                  <Typography sx={{ fontSize: 14, color: "text.secondary", mb: 1.5 }}>
                    {filter === "attention"
                      ? t("water.page.allSet")
                      : q
                        ? t("water.page.noSearchMatch")
                        : t("water.page.noLocations")}
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
                  addLabel={t("water.page.addLocationTo", { name: selectedDept.name })}
                  editableCodes={editableCodes}
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
        editableCodes={editableCodes}
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
        message={pendingDeleteDept ? t("water.page.deleteDeptConfirm", { name: pendingDeleteDept.name }) : ""}
        onCancel={() => setPendingDeleteDept(null)}
        onConfirm={() => pendingDeleteDept && deleteDept(pendingDeleteDept)}
      />
      <ConfirmationDialog
        destructive
        open={pendingDeletePoint != null}
        message={pendingDeletePoint ? t("water.page.deletePointConfirm", { code: pendingDeletePoint.code }) : ""}
        onCancel={() => setPendingDeletePoint(null)}
        onConfirm={() => pendingDeletePoint && deletePoint(pendingDeletePoint)}
      />

      {pasteCells && (
        <PastePreviewDialog
          open
          plan={pastePlan}
          cells={pasteCells}
          onAccept={handleAcceptPaste}
          onCancel={() => setPasteCells(null)}
        />
      )}
    </Box>
  );
}
