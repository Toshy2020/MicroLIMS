import { useCallback, useEffect, useMemo, useState } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../i18n";
import { Alert, Box, Button, IconButton, Paper, Stack, TextField, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ConfigMasterList, MasterListItem, SummaryTiles, ToneChip, frequencyLabel } from "../../../components/configHierarchy";
import { parseClipboardTable } from "../../../components/entry/parseClipboardTable";
import { PastePreviewDialog } from "../../../components/entry/PastePreviewDialog";
import type { PasteAcceptedRow } from "../../../components/entry/PastePreviewDialog";
import { PendingBatchList } from "../../../components/entry/PendingBatchList";
import type { PendingBatchColumn } from "../../../components/entry/PendingBatchList";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { monospaceFontFamily } from "../../../theme/palette";
import { getErrorMessage } from "../../../utils/errorMessage";
import { extractBatchSaveErrors } from "../../../utils/fieldErrors";
import { EMConfigService } from "./services/EMConfigService";
import { EmDepartment, EmRoom, RoomTestConfig, emTestTypeLabel } from "./emConfigTypes";
import { EmRoomCard } from "./components/EmRoomCard";
import { EmDepartmentPanel, EmRoomPanel, EmTestConfigPanel } from "./components/EmPanels";
import {
  buildEmRoomPastePlan,
  toCreateRoomPayload,
  toPendingEmRooms
} from "./emRoomPaste";
import type { PendingEmRoom } from "./emRoomPaste";
import {
  buildEmRoomTestConfigPastePlan,
  toCreateRoomTestConfigPayload,
  toPendingEmTestConfigs
} from "./emRoomTestConfigPaste";
import type { PendingEmTestConfig } from "./emRoomTestConfigPaste";

type PasteMode = "rooms" | "configs";

type PendingDelete =
  | { kind: "dept"; dept: EmDepartment }
  | { kind: "room"; room: EmRoom }
  | { kind: "config"; config: RoomTestConfig; room: EmRoom };

function getRoomPendingColumns(t: TFunction<"labConfig">): PendingBatchColumn<PendingEmRoom>[] {
  return [
  { key: "name", label: t("environmentalMonitoring.page.roomsColumns.name"), sx: { fontWeight: 600 }, render: (r) => r.name },
  { key: "departmentName", label: t("environmentalMonitoring.page.roomsColumns.department"), render: (r) => r.departmentName },
  { key: "gradeClassification", label: t("environmentalMonitoring.page.roomsColumns.grade"), render: (r) => t("environmentalMonitoring.grade", { grade: r.gradeClassification }) },
  { key: "kind", label: t("environmentalMonitoring.page.roomsColumns.type"), render: (r) => r.kind },
  { key: "parentRoomName", label: t("environmentalMonitoring.page.roomsColumns.sitsIn"), render: (r) => r.parentRoomName || "—" },
  { key: "frequencyLabelText", label: t("environmentalMonitoring.page.roomsColumns.frequency"), render: (r) => r.frequencyLabelText || t("environmentalMonitoring.page.sameAsDepartment") }
  ];
}

function getConfigPendingColumns(t: TFunction<"labConfig">): PendingBatchColumn<PendingEmTestConfig>[] {
  return [
  { key: "roomLabel", label: t("environmentalMonitoring.page.configColumns.room"), sx: { fontWeight: 600 }, render: (r) => r.roomLabel },
  { key: "testTypeLabel", label: t("environmentalMonitoring.page.configColumns.testType"), render: (r) => r.testTypeLabel },
  {
    key: "testCode",
    label: t("environmentalMonitoring.page.configColumns.test"),
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.testCode
  },
  {
    key: "alertLimit",
    label: t("environmentalMonitoring.page.configColumns.alert"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.alertLimit || "—"
  },
  {
    key: "actionLimit",
    label: t("environmentalMonitoring.page.configColumns.action"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.actionLimit || "—"
  },
  {
    key: "specLimit",
    label: t("environmentalMonitoring.page.configColumns.spec"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.specLimit || "—"
  },
  { key: "unit", label: t("environmentalMonitoring.page.configColumns.unit"), render: (r) => r.unit || "—" }
  ];
}

// Departments -> rooms (with grade) -> per-room test configurations and
// limits. Master/detail like the Water page: departments on the left,
// the selected department's rooms as cards on the right, every add/edit
// in a side panel.
export function EMConfigPage() {
  const { t } = useTranslation("labConfig");
  const roomPendingColumns = getRoomPendingColumns(t);
  const configPendingColumns = getConfigPendingColumns(t);
  const { options: testOptions } = useTestDefinitions();
  const [departments, setDepartments] = useState<EmDepartment[]>([]);
  const [configsByRoom, setConfigsByRoom] = useState<Record<number, RoomTestConfig[]>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [search, setSearch] = useState("");

  const [deptPanel, setDeptPanel] = useState<{ open: boolean; dept: EmDepartment | null }>({ open: false, dept: null });
  const [roomPanel, setRoomPanel] = useState<{ open: boolean; room: EmRoom | null }>({ open: false, room: null });
  const [configPanel, setConfigPanel] = useState<{ open: boolean; room: EmRoom | null; config: RoomTestConfig | null }>({ open: false, room: null, config: null });
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);

  const [pasteMode, setPasteMode] = useState<PasteMode | null>(null);
  const [pastePreview, setPastePreview] = useState<{ mode: PasteMode; cells: string[][] } | null>(null);

  const [pendingRooms, setPendingRooms] = useState<PendingEmRoom[]>([]);
  const [savingRooms, setSavingRooms] = useState(false);
  const [roomsBatchError, setRoomsBatchError] = useState<string | null>(null);

  const [pendingConfigs, setPendingConfigs] = useState<PendingEmTestConfig[]>([]);
  const [savingConfigs, setSavingConfigs] = useState(false);
  const [configsBatchError, setConfigsBatchError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const depts: EmDepartment[] = await EMConfigService.getDepartments();
      const rooms = depts.flatMap((d) => d.rooms ?? []);
      // No bulk endpoint - one request per room, in parallel.
      const configs = await Promise.all(
        rooms.map((r) => EMConfigService.getRoomTestConfigurations(r.id).catch(() => [] as RoomTestConfig[]))
      );
      setDepartments(depts);
      setConfigsByRoom(Object.fromEntries(rooms.map((r, i) => [r.id, configs[i] ?? []])));
      setLoadError(null);
      setSelectedDeptId((current) => (current != null && depts.some((d) => d.id === current) ? current : depts[0]?.id ?? null));
    } catch (e: unknown) {
      setLoadError(getErrorMessage(e, i18n.t("environmentalMonitoring.page.loadError", { ns: "labConfig" })));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const allRooms = useMemo(() => departments.flatMap((d) => d.rooms ?? []), [departments]);
  const roomsWithoutTests = allRooms.filter((r) => (configsByRoom[r.id] ?? []).length === 0).length;
  const configCount = Object.values(configsByRoom).reduce((n, list) => n + list.length, 0);

  const roomPastePlan = useMemo(() => buildEmRoomPastePlan({ departments }), [departments]);
  const configPastePlan = useMemo(
    () => buildEmRoomTestConfigPastePlan({ departments, configsByRoom, testOptions }),
    [departments, configsByRoom, testOptions]
  );

  const anyModalOpen =
    deptPanel.open ||
    roomPanel.open ||
    configPanel.open ||
    pendingDelete !== null ||
    pastePreview !== null;

  useEffect(() => {
    if (anyModalOpen || pasteMode === null) return;
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
      setPastePreview({ mode: pasteMode, cells: table });
    };
    document.addEventListener("paste", handleDocumentPaste);
    return () => document.removeEventListener("paste", handleDocumentPaste);
  }, [anyModalOpen, pasteMode]);

  const handlePasteInput = (e: ReactClipboardEvent<HTMLElement>) => {
    if (!pasteMode) return;
    const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
    if (table.length === 0) return;
    e.preventDefault();
    e.stopPropagation();
    setPastePreview({ mode: pasteMode, cells: table });
  };

  const handleAcceptPaste = (rows: PasteAcceptedRow[]) => {
    if (!pastePreview) return;
    if (pastePreview.mode === "rooms") {
      setPendingRooms(toPendingEmRooms(rows, departments));
      setRoomsBatchError(null);
    } else {
      setPendingConfigs(toPendingEmTestConfigs(rows, departments, testOptions));
      setConfigsBatchError(null);
    }
    setPastePreview(null);
    setPasteMode(null);
  };

  const saveRoomsBatch = async () => {
    if (pendingRooms.length === 0) return;
    setSavingRooms(true);
    setRoomsBatchError(null);
    try {
      const payload = pendingRooms.map(toCreateRoomPayload);
      await EMConfigService.createRoomsBatch(payload);
      const count = pendingRooms.length;
      setPendingRooms([]);
      setMessage({ text: t("environmentalMonitoring.page.roomsAdded", { rooms: t("environmentalMonitoring.roomCount", { count }) }), ok: true });
      await load();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        t("environmentalMonitoring.page.roomsBatchError")
      );
      if (rowErrors) {
        setPendingRooms((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setRoomsBatchError(summary);
    } finally {
      setSavingRooms(false);
    }
  };

  const saveConfigsBatch = async () => {
    if (pendingConfigs.length === 0) return;
    setSavingConfigs(true);
    setConfigsBatchError(null);
    try {
      const payload = pendingConfigs.map(toCreateRoomTestConfigPayload);
      await EMConfigService.createRoomTestConfigurationsBatch(payload);
      const count = pendingConfigs.length;
      setPendingConfigs([]);
      setMessage({ text: t("environmentalMonitoring.page.configsAdded", { configs: t("environmentalMonitoring.configCount", { count }) }), ok: true });
      await load();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        t("environmentalMonitoring.page.configsBatchError")
      );
      if (rowErrors) {
        setPendingConfigs((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setConfigsBatchError(summary);
    } finally {
      setSavingConfigs(false);
    }
  };

  const q = search.trim().toLowerCase();
  const matchesRoom = (r: EmRoom) => !q || r.name.toLowerCase().includes(q);

  const listItems: MasterListItem[] = departments
    .filter((d) => !q || d.name.toLowerCase().includes(q) || (d.rooms ?? []).some(matchesRoom))
    .map((d) => {
      const rooms = d.rooms ?? [];
      const empty = rooms.filter((r) => (configsByRoom[r.id] ?? []).length === 0).length;
      const details = [rooms.length === 0 ? t("environmentalMonitoring.page.noRoomsYet") : t("environmentalMonitoring.roomCount", { count: rooms.length }), frequencyLabel(d) || d.testingFrequency].filter(Boolean).join(" · ");
      return {
        id: d.id,
        title: d.name,
        subtitle: details,
        badge: rooms.length === 0 ? undefined : empty > 0 ? { label: t("environmentalMonitoring.roomsWithoutTests", { count: empty }), tone: "inconclusive" } : { label: t("environmentalMonitoring.page.complete"), tone: "notDetected" }
      };
    });

  const selectedDept = departments.find((d) => d.id === selectedDeptId) ?? null;
  const deptRooms = (selectedDept?.rooms ?? []).filter(matchesRoom);

  const afterSave = (text: string, selectId?: number) => {
    setDeptPanel({ open: false, dept: null });
    setRoomPanel({ open: false, room: null });
    setConfigPanel({ open: false, room: null, config: null });
    setMessage({ text, ok: true });
    if (selectId != null) setSelectedDeptId(selectId);
    load();
  };

  const confirmDelete = async () => {
    const target = pendingDelete;
    setPendingDelete(null);
    if (!target) return;
    setMessage(null);
    try {
      if (target.kind === "dept") {
        await EMConfigService.deleteDepartment(target.dept.id);
        setMessage({ text: t("environmentalMonitoring.page.deptDeleted", { name: target.dept.name }), ok: true });
      } else if (target.kind === "room") {
        await EMConfigService.deleteRoom(target.room.id);
        setMessage({ text: t("environmentalMonitoring.page.roomDeleted", { name: target.room.name }), ok: true });
      } else {
        await EMConfigService.deleteRoomTestConfiguration(target.config.id);
        setMessage({ text: t("environmentalMonitoring.page.testRemoved", { room: target.room.name }), ok: true });
      }
      load();
    } catch (e: unknown) {
      setMessage({ text: getErrorMessage(e, t("environmentalMonitoring.page.deleteError")), ok: false });
    }
  };

  const deleteMessage = !pendingDelete
    ? ""
    : pendingDelete.kind === "dept"
      ? t("environmentalMonitoring.page.deleteDept", { name: pendingDelete.dept.name })
      : pendingDelete.kind === "room"
        ? t("environmentalMonitoring.page.deleteRoom", { name: pendingDelete.room.name })
        : t("environmentalMonitoring.page.removeTest", {
          type: t(`environmentalMonitoring.testTypes.${pendingDelete.config.testType}`, { defaultValue: emTestTypeLabel(pendingDelete.config.testType) }),
          code: pendingDelete.config.testCode,
          room: pendingDelete.room.name
        });

  const pasteBarHelper =
    pasteMode === "rooms"
      ? t("environmentalMonitoring.page.pasteRoomsHelper")
      : t("environmentalMonitoring.page.pasteConfigsHelper");

  const pasteBarPlaceholder =
    pasteMode === "rooms"
      ? t("environmentalMonitoring.page.pasteRoomsPlaceholder")
      : t("environmentalMonitoring.page.pasteConfigsPlaceholder");

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title={t("environmentalMonitoring.page.title")} subtitle={t("environmentalMonitoring.page.subtitle")} />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            {t("environmentalMonitoring.page.addDepartment")}
          </Button>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            disabled={departments.length === 0}
            aria-expanded={pasteMode === "rooms"}
            onClick={() => setPasteMode((m) => (m === "rooms" ? null : "rooms"))}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            {t("environmentalMonitoring.page.pasteRooms")}
          </Button>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            disabled={allRooms.length === 0}
            aria-expanded={pasteMode === "configs"}
            onClick={() => setPasteMode((m) => (m === "configs" ? null : "configs"))}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            {t("environmentalMonitoring.page.pasteConfigs")}
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            disabled={departments.length === 0}
            onClick={() => setRoomPanel({ open: true, room: null })}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            {t("environmentalMonitoring.page.addRoom")}
          </Button>
        </Stack>
      </Stack>

      {pasteMode !== null && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2, borderRadius: 2 }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
            <TextField
              autoFocus
              size="small"
              fullWidth
              label={pasteMode === "rooms" ? t("environmentalMonitoring.page.pasteRoomsLabel") : t("environmentalMonitoring.page.pasteConfigsLabel")}
              placeholder={pasteBarPlaceholder}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={pasteBarHelper}
            />
            <Button onClick={() => setPasteMode(null)} sx={{ textTransform: "none", flexShrink: 0 }}>
              {t("environmentalMonitoring.page.close")}
            </Button>
          </Stack>
        </Paper>
      )}

      {loadError && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={load}>{t("environmentalMonitoring.page.retry")}</Button>}>
          {loadError}
        </Alert>
      )}
      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <PendingBatchList
        title={t("environmentalMonitoring.page.pendingRoomsTitle", { count: pendingRooms.length })}
        subtitle={t("environmentalMonitoring.page.pendingSubtitle", { items: t("environmentalMonitoring.roomCount", { count: pendingRooms.length }) })}
        ariaLabel={t("environmentalMonitoring.page.pendingRoomsAria")}
        columns={roomPendingColumns}
        rows={pendingRooms}
        getRowKey={(r, i) => `${r.departmentId}-${r.name}-${i}`}
        getRowLabel={(r) => r.name}
        saving={savingRooms}
        error={roomsBatchError}
        onSave={saveRoomsBatch}
        onDiscard={() => {
          setPendingRooms([]);
          setRoomsBatchError(null);
        }}
        onRemoveRow={(index) => {
          setPendingRooms((prev) => prev.filter((_, i) => i !== index));
          setRoomsBatchError(null);
        }}
      />

      <PendingBatchList
        title={t("environmentalMonitoring.page.pendingConfigsTitle", { count: pendingConfigs.length })}
        subtitle={t("environmentalMonitoring.page.pendingSubtitle", { items: t("environmentalMonitoring.configCount", { count: pendingConfigs.length }) })}
        ariaLabel={t("environmentalMonitoring.page.pendingConfigsAria")}
        columns={configPendingColumns}
        rows={pendingConfigs}
        getRowKey={(r, i) => `${r.roomId}-${r.testType}-${r.testCode}-${i}`}
        getRowLabel={(r) => `${r.roomName} ${r.testCode}`}
        saving={savingConfigs}
        error={configsBatchError}
        onSave={saveConfigsBatch}
        onDiscard={() => {
          setPendingConfigs([]);
          setConfigsBatchError(null);
        }}
        onRemoveRow={(index) => {
          setPendingConfigs((prev) => prev.filter((_, i) => i !== index));
          setConfigsBatchError(null);
        }}
      />

      <SummaryTiles
        tiles={[
          { label: t("environmentalMonitoring.page.tiles.departments"), value: departments.length },
          { label: t("environmentalMonitoring.page.tiles.rooms"), value: allRooms.length },
          { label: t("environmentalMonitoring.page.tiles.testsConfigured"), value: configCount },
          { label: t("environmentalMonitoring.page.tiles.roomsWithoutTests"), value: t("environmentalMonitoring.roomCount", { count: roomsWithoutTests }), tone: roomsWithoutTests > 0 ? "inconclusive" : undefined }
        ]}
      />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "300px minmax(0, 1fr)" }, gap: 2.5, alignItems: "start" }}>
        <ConfigMasterList
          searchLabel={t("environmentalMonitoring.page.searchLabel")}
          searchPlaceholder={t("environmentalMonitoring.page.searchPlaceholder")}
          search={search}
          onSearchChange={setSearch}
          items={listItems}
          selectedId={selectedDeptId}
          onSelect={setSelectedDeptId}
          loading={loading}
          emptyText={q ? t("environmentalMonitoring.page.noMatch") : t("environmentalMonitoring.page.noDepartments")}
        />

        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
          {!selectedDept ? (
            <Box sx={{ p: 4, textAlign: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{loading ? t("environmentalMonitoring.page.loading") : t("environmentalMonitoring.page.selectDepartment")}</Typography>
            </Box>
          ) : (
            <>
              <Stack direction="row" sx={{ px: 2.75, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700 }}>{selectedDept.name}</Typography>
                  <Stack useFlexGap direction="row" spacing={0.75} sx={{ mt: 0.5, alignItems: "center", flexWrap: "wrap" }}>
                    {selectedDept.class && <ToneChip label={selectedDept.class} tone="pending" />}
                    {(frequencyLabel(selectedDept) || selectedDept.testingFrequency) && <ToneChip label={t("environmentalMonitoring.page.tested", { frequency: (frequencyLabel(selectedDept) || selectedDept.testingFrequency).toLowerCase() })} tone="pending" />}
                    <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("environmentalMonitoring.roomCount", { count: (selectedDept.rooms ?? []).length })}</Typography>
                  </Stack>
                </Box>
                <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: selectedDept })}>
                  {t("environmentalMonitoring.page.editDepartment")}
                </Button>
                <Tooltip title={t("environmentalMonitoring.page.deleteDepartment")}>
                  <IconButton aria-label={t("environmentalMonitoring.page.deleteAria", { name: selectedDept.name })} color="error" onClick={() => setPendingDelete({ kind: "dept", dept: selectedDept })}>
                    <DeleteIcon />
                  </IconButton>
                </Tooltip>
              </Stack>

              <Box sx={{ p: 2.75, display: "grid", gridTemplateColumns: { xs: "1fr", lg: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
                {deptRooms.map((room) => (
                  <EmRoomCard
                    key={room.id}
                    room={room}
                    deptFrequency={selectedDept}
                    configs={configsByRoom[room.id] ?? []}
                    onEditRoom={() => setRoomPanel({ open: true, room: { ...room, departmentId: selectedDept.id } })}
                    onDeleteRoom={() => setPendingDelete({ kind: "room", room })}
                    onAddConfig={() => setConfigPanel({ open: true, room, config: null })}
                    onEditConfig={(config) => setConfigPanel({ open: true, room, config })}
                    onDeleteConfig={(config) => setPendingDelete({ kind: "config", config, room })}
                  />
                ))}
                <Button
                  variant="outlined"
                  startIcon={<AddIcon />}
                  onClick={() => setRoomPanel({ open: true, room: null })}
                  sx={{ minHeight: 120, borderStyle: "dashed", borderRadius: 2, textTransform: "none", fontWeight: 600 }}
                >
                  {t("environmentalMonitoring.page.addRoomTo", { name: selectedDept.name })}
                </Button>
              </Box>
              {q && deptRooms.length === 0 && (
                <Typography sx={{ px: 2.75, pb: 2.75, fontSize: 14, color: "text.secondary" }}>{t("environmentalMonitoring.page.noRoomMatch")}</Typography>
              )}
            </>
          )}
        </Paper>
      </Box>

      <EmDepartmentPanel open={deptPanel.open} dept={deptPanel.dept} onClose={() => setDeptPanel({ open: false, dept: null })} onSaved={afterSave} />
      <EmRoomPanel
        open={roomPanel.open}
        room={roomPanel.room}
        defaultDepartmentId={selectedDeptId}
        departments={departments}
        onClose={() => setRoomPanel({ open: false, room: null })}
        onSaved={afterSave}
      />
      <EmTestConfigPanel
        open={configPanel.open}
        room={configPanel.room}
        config={configPanel.config}
        onClose={() => setConfigPanel({ open: false, room: null, config: null })}
        onSaved={(text) => afterSave(text)}
      />

      <ConfirmationDialog
        destructive
        open={pendingDelete != null}
        message={deleteMessage}
        onCancel={() => setPendingDelete(null)}
        onConfirm={confirmDelete}
      />

      {pastePreview && (
        <PastePreviewDialog
          open
          plan={pastePreview.mode === "rooms" ? roomPastePlan : configPastePlan}
          cells={pastePreview.cells}
          onAccept={handleAcceptPaste}
          onCancel={() => setPastePreview(null)}
        />
      )}
    </Box>
  );
}
