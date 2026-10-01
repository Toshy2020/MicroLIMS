import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ConfigMasterList, MasterListItem, SummaryTiles, ToneChip } from "../../../components/configHierarchy";
import { EMConfigService } from "./services/EMConfigService";
import { EmDepartment, EmRoom, RoomTestConfig, emTestTypeLabel } from "./emConfigTypes";
import { EmRoomCard } from "./components/EmRoomCard";
import { EmDepartmentPanel, EmRoomPanel, EmTestConfigPanel } from "./components/EmPanels";

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

type PendingDelete =
  | { kind: "dept"; dept: EmDepartment }
  | { kind: "room"; room: EmRoom }
  | { kind: "config"; config: RoomTestConfig; room: EmRoom };

// Departments -> rooms (with grade) -> per-room test configurations and
// limits. Master/detail like the Water page: departments on the left,
// the selected department's rooms as cards on the right, every add/edit
// in a side panel.
export function EMConfigPage() {
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
    } catch (e: any) {
      setLoadError(e?.response?.data?.message ?? "Could not load the environmental monitoring configuration.");
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

  const q = search.trim().toLowerCase();
  const matchesRoom = (r: EmRoom) => !q || r.name.toLowerCase().includes(q);

  const listItems: MasterListItem[] = departments
    .filter((d) => !q || d.name.toLowerCase().includes(q) || (d.rooms ?? []).some(matchesRoom))
    .map((d) => {
      const rooms = d.rooms ?? [];
      const empty = rooms.filter((r) => (configsByRoom[r.id] ?? []).length === 0).length;
      const details = [rooms.length === 0 ? "No rooms yet" : plural(rooms.length, "room"), d.testingFrequency].filter(Boolean).join(" · ");
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
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this record.", ok: false });
    }
  };

  const deleteMessage = !pendingDelete
    ? ""
    : pendingDelete.kind === "dept"
      ? `Delete department "${pendingDelete.dept.name}"? This cannot be undone.`
      : pendingDelete.kind === "room"
        ? `Delete room "${pendingDelete.room.name}"? This cannot be undone.`
        : `Remove the ${emTestTypeLabel(pendingDelete.config.testType)} / ${pendingDelete.config.testCode} test from ${pendingDelete.room.name}?`;

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title="Environmental Monitoring" subtitle="Departments, their rooms and grades, and per-room test limits." />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setDeptPanel({ open: true, dept: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            Add department
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
                  <Stack direction="row" spacing={0.75} sx={{ mt: 0.5, alignItems: "center", flexWrap: "wrap" }}>
                    {selectedDept.class && <ToneChip label={selectedDept.class} tone="pending" />}
                    {selectedDept.testingFrequency && <ToneChip label={`Tested ${selectedDept.testingFrequency.toLowerCase()}`} tone="pending" />}
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
    </Box>
  );
}
