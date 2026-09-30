import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ConfigMasterList, MasterListItem, SummaryTiles } from "../../../components/configHierarchy";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { AfterCleaningConfigService } from "./services/AfterCleaningConfigService";
import { Machine, MachinePart, PartConfig, acTestTypeLabel, isPathogenConfig } from "./acConfigTypes";
import { AcPartCard } from "./components/AcPartCard";
import { AcCountTestPanel, AcMachinePanel, AcPartPanel, AcPathogenPanel } from "./components/AcPanels";

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

type PendingDelete =
  | { kind: "machine"; machine: Machine }
  | { kind: "part"; part: MachinePart }
  | { kind: "config"; config: PartConfig; part: MachinePart };

// Machines -> parts -> per-part tests: swab/rinse count tests with limits,
// and any number of pathogen tests. Master/detail like the Water and EM
// pages: machines on the left, the selected machine's parts as cards on
// the right, every add/edit in a side panel.
export function AfterCleaningConfigPage() {
  const { options } = useTestDefinitions();
  const [machines, setMachines] = useState<Machine[]>([]);
  const [configsByPart, setConfigsByPart] = useState<Record<number, PartConfig[]>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [selectedMachineId, setSelectedMachineId] = useState<number | null>(null);
  const [search, setSearch] = useState("");

  const [machinePanel, setMachinePanel] = useState<{ open: boolean; machine: Machine | null }>({ open: false, machine: null });
  const [partPanel, setPartPanel] = useState<{ open: boolean; part: MachinePart | null }>({ open: false, part: null });
  const [countPanel, setCountPanel] = useState<{ open: boolean; part: MachinePart | null; config: PartConfig | null }>({ open: false, part: null, config: null });
  const [pathogenPanel, setPathogenPanel] = useState<{ open: boolean; part: MachinePart | null }>({ open: false, part: null });
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const list: Machine[] = await AfterCleaningConfigService.getMachines();
      const parts = list.flatMap((m) => m.parts ?? []);
      // No bulk endpoint - one request per part, in parallel.
      const configs = await Promise.all(
        parts.map((p) => AfterCleaningConfigService.getPartConfigurations(p.id).catch(() => [] as PartConfig[]))
      );
      setMachines(list);
      setConfigsByPart(Object.fromEntries(parts.map((p, i) => [p.id, configs[i] ?? []])));
      setLoadError(null);
      setSelectedMachineId((current) => (current != null && list.some((m) => m.id === current) ? current : list[0]?.id ?? null));
    } catch (e: any) {
      setLoadError(e?.response?.data?.message ?? "Could not load the after-cleaning configuration.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const testName = useCallback(
    (code: string) => {
      const o = options.find((x) => x.code === code);
      return o?.displayName && o.displayName !== code ? o.displayName : code;
    },
    [options]
  );

  const allParts = useMemo(() => machines.flatMap((m) => m.parts ?? []), [machines]);
  const allConfigs = Object.values(configsByPart).flat();
  const partsWithoutTests = allParts.filter((p) => (configsByPart[p.id] ?? []).length === 0).length;
  const pathogenCount = allConfigs.filter(isPathogenConfig).length;

  const q = search.trim().toLowerCase();
  const matchesPart = (p: MachinePart) => !q || p.name.toLowerCase().includes(q);

  const listItems: MasterListItem[] = machines
    .filter((m) => !q || m.name.toLowerCase().includes(q) || (m.parts ?? []).some(matchesPart))
    .map((m) => {
      const parts = m.parts ?? [];
      const empty = parts.filter((p) => (configsByPart[p.id] ?? []).length === 0).length;
      return {
        id: m.id,
        title: m.name,
        subtitle: parts.length === 0 ? "No parts yet" : plural(parts.length, "part"),
        badge: parts.length === 0 ? undefined : empty > 0 ? { label: `${plural(empty, "part")} without tests`, tone: "inconclusive" } : { label: "Complete", tone: "notDetected" }
      };
    });

  // Memoized: the panel resets its selection whenever this list changes.
  const pathogenPanelConfigs = useMemo(
    () => (pathogenPanel.part ? (configsByPart[pathogenPanel.part.id] ?? []).filter(isPathogenConfig) : []),
    [pathogenPanel.part, configsByPart]
  );

  const selectedMachine = machines.find((m) => m.id === selectedMachineId) ?? null;
  const machineParts = (selectedMachine?.parts ?? []).filter(matchesPart);

  const closeAll = () => {
    setMachinePanel({ open: false, machine: null });
    setPartPanel({ open: false, part: null });
    setCountPanel({ open: false, part: null, config: null });
    setPathogenPanel({ open: false, part: null });
  };

  const afterSave = (text: string, selectId?: number) => {
    closeAll();
    setMessage({ text, ok: true });
    if (selectId != null) setSelectedMachineId(selectId);
    load();
  };

  const confirmDelete = async () => {
    const target = pendingDelete;
    setPendingDelete(null);
    if (!target) return;
    setMessage(null);
    try {
      if (target.kind === "machine") {
        await AfterCleaningConfigService.deleteMachine(target.machine.id);
        setMessage({ text: `"${target.machine.name}" deleted.`, ok: true });
      } else if (target.kind === "part") {
        await AfterCleaningConfigService.deleteMachinePart(target.part.id);
        setMessage({ text: `Part "${target.part.name}" deleted.`, ok: true });
      } else {
        await AfterCleaningConfigService.deletePartConfiguration(target.config.id);
        setMessage({ text: `Test removed from ${target.part.name}.`, ok: true });
      }
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this record.", ok: false });
    }
  };

  const deleteMessage = !pendingDelete
    ? ""
    : pendingDelete.kind === "machine"
      ? `Delete machine "${pendingDelete.machine.name}"? This cannot be undone.`
      : pendingDelete.kind === "part"
        ? `Delete part "${pendingDelete.part.name}"? This cannot be undone.`
        : `Remove the ${acTestTypeLabel(pendingDelete.config.testType)} / ${pendingDelete.config.testCode} test from ${pendingDelete.part.name}?`;

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <PageHeader title="After Cleaning" subtitle="Machines, their parts, and the swab, rinse and pathogen tests run on each part." />
        <Stack direction="row" sx={{ gap: 1.5, flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => setMachinePanel({ open: true, machine: null })} sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}>
            Add machine
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            disabled={machines.length === 0}
            onClick={() => setPartPanel({ open: true, part: null })}
            sx={{ textTransform: "none", fontWeight: 700, whiteSpace: "nowrap" }}
          >
            Add machine part
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
          { label: "Machines", value: machines.length },
          { label: "Machine parts", value: allParts.length },
          { label: "Pathogen tests", value: pathogenCount },
          { label: "Parts without tests", value: plural(partsWithoutTests, "part"), tone: partsWithoutTests > 0 ? "inconclusive" : undefined }
        ]}
      />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "300px minmax(0, 1fr)" }, gap: 2.5, alignItems: "start" }}>
        <ConfigMasterList
          searchLabel="Find a machine or part"
          searchPlaceholder="e.g. Filling needles"
          search={search}
          onSearchChange={setSearch}
          items={listItems}
          selectedId={selectedMachineId}
          onSelect={setSelectedMachineId}
          loading={loading}
          emptyText={q ? "Nothing matches your search." : "No machines yet. Use “Add machine” to create one."}
        />

        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden" }}>
          {!selectedMachine ? (
            <Box sx={{ p: 4, textAlign: "center" }}>
              <Typography sx={{ fontWeight: 600 }}>{loading ? "Loading..." : "Select a machine"}</Typography>
            </Box>
          ) : (
            <>
              <Stack direction="row" sx={{ px: 2.75, py: 2.25, borderBottom: "1px solid", borderColor: "divider", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography component="h2" sx={{ fontSize: 20, fontWeight: 700 }}>{selectedMachine.name}</Typography>
                  <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{plural((selectedMachine.parts ?? []).length, "part")}</Typography>
                </Box>
                <Button variant="outlined" onClick={() => setMachinePanel({ open: true, machine: selectedMachine })} sx={{ textTransform: "none" }}>
                  Rename
                </Button>
                <Tooltip title="Delete machine">
                  <IconButton aria-label={`Delete ${selectedMachine.name}`} color="error" onClick={() => setPendingDelete({ kind: "machine", machine: selectedMachine })}>
                    <DeleteIcon />
                  </IconButton>
                </Tooltip>
              </Stack>

              <Box sx={{ p: 2.75, display: "grid", gridTemplateColumns: { xs: "1fr", lg: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
                {machineParts.map((part) => {
                  const configs = configsByPart[part.id] ?? [];
                  return (
                    <AcPartCard
                      key={part.id}
                      part={part}
                      configs={configs}
                      testName={testName}
                      onEditPart={() => setPartPanel({ open: true, part: { ...part, machineId: selectedMachine.id } })}
                      onDeletePart={() => setPendingDelete({ kind: "part", part })}
                      onAddCount={() => setCountPanel({ open: true, part, config: null })}
                      onEditCount={(config) => setCountPanel({ open: true, part, config })}
                      onDeleteCount={(config) => setPendingDelete({ kind: "config", config, part })}
                      onEditPathogens={() => setPathogenPanel({ open: true, part })}
                    />
                  );
                })}
                <Button
                  variant="outlined"
                  startIcon={<AddIcon />}
                  onClick={() => setPartPanel({ open: true, part: null })}
                  sx={{ minHeight: 120, borderStyle: "dashed", borderRadius: 2, textTransform: "none", fontWeight: 600 }}
                >
                  Add part to {selectedMachine.name}
                </Button>
              </Box>
              {q && machineParts.length === 0 && (
                <Typography sx={{ px: 2.75, pb: 2.75, fontSize: 14, color: "text.secondary" }}>No parts here match your search.</Typography>
              )}
            </>
          )}
        </Paper>
      </Box>

      <AcMachinePanel open={machinePanel.open} machine={machinePanel.machine} onClose={() => setMachinePanel({ open: false, machine: null })} onSaved={afterSave} />
      <AcPartPanel
        open={partPanel.open}
        part={partPanel.part}
        defaultMachineId={selectedMachineId}
        machines={machines}
        onClose={() => setPartPanel({ open: false, part: null })}
        onSaved={afterSave}
      />
      <AcCountTestPanel
        open={countPanel.open}
        part={countPanel.part}
        config={countPanel.config}
        onClose={() => setCountPanel({ open: false, part: null, config: null })}
        onSaved={(text) => afterSave(text)}
      />
      <AcPathogenPanel
        open={pathogenPanel.open}
        part={pathogenPanel.part}
        configs={pathogenPanelConfigs}
        onClose={() => {
          setPathogenPanel({ open: false, part: null });
          // A partly failed save may already have added or removed some.
          load();
        }}
        onSaved={(text) => afterSave(text)}
      />

      <ConfirmationDialog destructive open={pendingDelete != null} message={deleteMessage} onCancel={() => setPendingDelete(null)} onConfirm={confirmDelete} />
    </Box>
  );
}
