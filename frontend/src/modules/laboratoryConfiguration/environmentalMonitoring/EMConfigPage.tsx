import { useCallback, useEffect, useMemo, useState } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
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

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

type PasteMode = "rooms" | "configs";

type PendingDelete =
  | { kind: "dept"; dept: EmDepartment }
  | { kind: "room"; room: EmRoom }
  | { kind: "config"; config: RoomTestConfig; room: EmRoom };

const ROOM_PENDING_COLUMNS: PendingBatchColumn<PendingEmRoom>[] = [
  { key: "name", label: "Room name", sx: { fontWeight: 600 }, render: (r) => r.name },
  { key: "departmentName", label: "Department", render: (r) => r.departmentName },
  { key: "gradeClassification", label: "Grade", render: (r) => `Grade ${r.gradeClassification}` },
  { key: "kind", label: "Type", render: (r) => r.kind },
  { key: "parentRoomName", label: "Sits in room", render: (r) => r.parentRoomName || "—" },
  { key: "frequencyLabelText", label: "Frequency", render: (r) => r.frequencyLabelText || "Same as department" }
];

const CONFIG_PENDING_COLUMNS: PendingBatchColumn<PendingEmTestConfig>[] = [
  { key: "roomLabel", label: "Room", sx: { fontWeight: 600 }, render: (r) => r.roomLabel },
  { key: "testTypeLabel", label: "Test type", render: (r) => r.testTypeLabel },
  {
    key: "testCode",
    label: "Test",
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.testCode
  },
  {
    key: "alertLimit",
    label: "Alert limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.alertLimit || "—"
  },
  {
    key: "actionLimit",
    label: "Action limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.actionLimit || "—"
  },
  {
    key: "specLimit",
    label: "Spec limit",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.specLimit || "—"
  },
  { key: "unit", label: "Unit", render: (r) => r.unit || "—" }
];

// Departments -> rooms (with grade) -> per-room test configurations and
// limits. Master/detail like the Water page: departments on the left,
// the selected department's rooms as cards on the right, every add/edit
// in a side panel.
export function EMConfigPage() {
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
      setLoadError(getErrorMessage(e, "Could not load the environmental monitoring configuration."));
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
      setMessage({ text: `${plural(count, "room")} added.`, ok: true });
      await load();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        "Could not save rooms. Nothing was created."
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
      setMessage({ text: `${plural(count, "test configuration")} added.`, ok: true });
      await load();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        "Could not save test configurations. Nothing was created."
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
      const details = [rooms.length === 0 ? "No rooms yet" : plural(rooms.length, "room"), frequencyLabel(d) || d.testingFrequency].filter(Boolean).join(" · ");
      return {
        id: d.id,
        title: d.name,
        subtitle: details,
        badge: rooms.length === 0 ? undefined : empty > 0 ? { label: `${plural(empty, "room")} without tests`, tone: "inconclusive" } : { label: "Complete", tone: "notDetected" }
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
        setMessage({ text: `"${target.dept.name}" deleted.`, ok: true });
      } else if (target.kind === "room") {
        await EMConfigService.deleteRoom(target.room.id);
        setMessage({ text: `Room "${target.room.name}" deleted.`, ok: true });
      } else {
        await EMConfigService.deleteRoomTestConfiguration(target.config.id);
        setMessage({ text: `Test removed from ${target.room.name}.`, ok: true });
      }
      load();
    } catch (e: unknown) {
      setMessage({ text: getErrorMessage(e, "Could not delete this record."), ok: false });
    }
  };

  const deleteMessage = !pendingDelete
    ? ""
    : pendingDelete.kind === "dept"
      ? `Delete department "${pendingDelete.dept.name}"? This cannot be undone.`
      : pendingDelete.kind === "room"
        ? `Delete room "${pendingDelete.room.name}"? This cannot be undone.`
        : `Remove the ${emTestTypeLabel(pendingDelete.config.testType)} / ${pendingDelete.config.testCode} test from ${pendingDelete.room.name}?`;

  const pasteBarHelper =
    pasteMode === "rooms"
      ? "Columns in order: Room name, Department, Grade, Type (Room or LAF, optional), Sits in room (required for LAF), Frequency (optional)."
      : "Columns in order: Room (or Department / Room), Test type, Test, Alert limit, Action limit, Spec limit, Unit (optional).";

  const pasteBarPlaceholder =
    pasteMode === "rooms"
      ? "Paste rows copied from a spreadsheet (Room name, Department, Grade, Type, Sits in room, Frequency)"
      : "Paste rows copied from a spreadsheet (Room, Test type, Test, Alert limit, Action limit, Spec limit, Unit)";

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title="Environmental Monitoring" subtitle="Departments, their rooms and grades, and per-room test limits." />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            Add department
          </Button>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            disabled={departments.length === 0}
            aria-expanded={pasteMode === "rooms"}
            onClick={() => setPasteMode((m) => (m === "rooms" ? null : "rooms"))}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            Paste rooms
          </Button>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            disabled={allRooms.length === 0}
            aria-expanded={pasteMode === "configs"}
            onClick={() => setPasteMode((m) => (m === "configs" ? null : "configs"))}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            Paste test configurations
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            disabled={departments.length === 0}
            onClick={() => setRoomPanel({ open: true, room: null })}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            Add room
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
              label={pasteMode === "rooms" ? "Paste room rows" : "Paste test configuration rows"}
              placeholder={pasteBarPlaceholder}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={pasteBarHelper}
            />
            <Button onClick={() => setPasteMode(null)} sx={{ textTransform: "none", flexShrink: 0 }}>
              Close
            </Button>
          </Stack>
        </Paper>
      )}

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

      <PendingBatchList
        title={`Pending rooms (${pendingRooms.length})`}
        subtitle={`Review the ${plural(pendingRooms.length, "room")} below. Nothing is saved until you click Save.`}
        ariaLabel="Pending rooms"
        columns={ROOM_PENDING_COLUMNS}
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
        title={`Pending test configurations (${pendingConfigs.length})`}
        subtitle={`Review the ${plural(pendingConfigs.length, "test configuration")} below. Nothing is saved until you click Save.`}
        ariaLabel="Pending test configurations"
        columns={CONFIG_PENDING_COLUMNS}
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
          { label: "Departments", value: departments.length },
          { label: "Rooms", value: allRooms.length },
          { label: "Tests configured", value: configCount },
          { label: "Rooms without tests", value: plural(roomsWithoutTests, "room"), tone: roomsWithoutTests > 0 ? "inconclusive" : undefined }
        ]}
      />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "300px minmax(0, 1fr)" }, gap: 2.5, alignItems: "start" }}>
        <ConfigMasterList
          searchLabel="Find a department or room"
          searchPlaceholder="e.g. Filling"
          search={search}
          onSearchChange={setSearch}
          items={listItems}
          selectedId={selectedDeptId}
          onSelect={setSelectedDeptId}
          loading={loading}
          emptyText={q ? "Nothing matches your search." : "No departments yet. Use “Add department” to create one."}
        />

        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
          {!selectedDept ? (
            <Box sx={{ p: 4, textAlign: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{loading ? "Loading..." : "Select a department"}</Typography>
            </Box>
          ) : (
            <>
              <Stack direction="row" sx={{ px: 2.75, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700 }}>{selectedDept.name}</Typography>
                  <Stack useFlexGap direction="row" spacing={0.75} sx={{ mt: 0.5, alignItems: "center", flexWrap: "wrap" }}>
                    {selectedDept.class && <ToneChip label={selectedDept.class} tone="pending" />}
                    {(frequencyLabel(selectedDept) || selectedDept.testingFrequency) && <ToneChip label={`Tested ${(frequencyLabel(selectedDept) || selectedDept.testingFrequency).toLowerCase()}`} tone="pending" />}
                    <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{plural((selectedDept.rooms ?? []).length, "room")}</Typography>
                  </Stack>
                </Box>
                <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: selectedDept })}>
                  Edit department
                </Button>
                <Tooltip title="Delete department">
                  <IconButton aria-label={`Delete ${selectedDept.name}`} color="error" onClick={() => setPendingDelete({ kind: "dept", dept: selectedDept })}>
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
                  Add room to {selectedDept.name}
                </Button>
              </Box>
              {q && deptRooms.length === 0 && (
                <Typography sx={{ px: 2.75, pb: 2.75, fontSize: 14, color: "text.secondary" }}>No rooms here match your search.</Typography>
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
