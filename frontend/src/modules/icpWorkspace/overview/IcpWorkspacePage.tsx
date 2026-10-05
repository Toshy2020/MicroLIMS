import { useState, useEffect, useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Tabs,
  Tab,
  Button,
  Alert,
  Skeleton
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import ScienceIcon from "@mui/icons-material/Science";
import { LabPage, KpiStrip, FilterBar, EmptyState } from "../../../components/lab";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { ICP_ROUTES } from "../routes";
import { IcpInstrumentCard } from "./IcpInstrumentCard";
import { StartIcpRunDialog } from "../run/StartIcpRunDialog";
import type { IcpInstrumentDto, IcpRunDto } from "../types";

export function IcpWorkspacePage() {
  const navigate = useNavigate();
  const { permissions, role } = useAuth();
  const canOperate =
    permissions.includes(PERMISSIONS.HPLC_OPERATE) ||
    role === "SystemAdministrator";

  const [instruments, setInstruments] = useState<IcpInstrumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [stateFilter, setStateFilter] = useState<string>("all");

  // Start run dialog state
  const [startDialogOpen, setStartDialogOpen] = useState(false);
  const [targetEquipmentId, setTargetEquipmentId] = useState<number | null>(null);

  const loadInstruments = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await IcpWorkspaceService.getInstruments();
      setInstruments(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load ICP instruments.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadInstruments();
  }, [loadInstruments]);

  // Summary counts for KPI Strip
  const counts = useMemo(() => {
    let running = 0;
    let available = 0;
    let unavailable = 0;
    let calPending = 0;
    instruments.forEach((inst) => {
      const s = inst.state.toLowerCase();
      if (s === "running") {
        running++;
        if (inst.activeRun?.calibrationStatus === "Pending") calPending++;
      } else if (s === "available") {
        available++;
      } else if (s === "unavailable") {
        unavailable++;
      }
    });
    return {
      total: instruments.length,
      running,
      available,
      unavailable,
      calPending
    };
  }, [instruments]);

  // Filtered instruments
  const filteredInstruments = useMemo(() => {
    return instruments.filter((inst) => {
      const matchesSearch =
        !search.trim() ||
        inst.name.toLowerCase().includes(search.toLowerCase()) ||
        inst.code.toLowerCase().includes(search.toLowerCase()) ||
        (inst.activeRun?.code.toLowerCase().includes(search.toLowerCase()) ?? false) ||
        (inst.activeRun?.methodAbbreviation.toLowerCase().includes(search.toLowerCase()) ?? false);

      const s = inst.state.toLowerCase();
      const matchesFilter = stateFilter === "all" || s === stateFilter;

      return matchesSearch && matchesFilter;
    });
  }, [instruments, search, stateFilter]);

  const gridSx = {
    display: "grid",
    gridTemplateColumns: { xs: "minmax(0, 1fr)", sm: "repeat(2, minmax(0, 1fr))", lg: "repeat(3, minmax(0, 1fr))" },
    gap: 2,
    alignItems: "stretch"
  } as const;

  const targetInstrument = instruments.find((i) => i.equipmentId === targetEquipmentId);

  return (
    <LabPage
      title="ICP-OES Workspace"
      subtitle="Overview of inductively coupled plasma optical emission spectrometers, active analytical runs, and calibrations."
      actions={
        <Button
          variant="outlined"
          size="small"
          startIcon={<RefreshIcon />}
          onClick={loadInstruments}
          disabled={loading}
          sx={{ textTransform: "none" }}
        >
          Refresh
        </Button>
      }
      kpis={
        <KpiStrip
          loading={loading}
          tiles={[
            { label: "Instruments", value: counts.total },
            { label: "Available", value: counts.available, tone: "notDetected" },
            { label: "In use", value: counts.running, tone: "info" },
            { label: "Cal. pending", value: counts.calPending, tone: counts.calPending > 0 ? "pending" : undefined },
            { label: "Unavailable", value: counts.unavailable, tone: counts.unavailable > 0 ? "detected" : undefined }
          ]}
        />
      }
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder="Search by code, name, run or method..."
          resultCount={filteredInstruments.length}
        >
          <Tabs
            value={stateFilter}
            onChange={(_, val) => setStateFilter(val)}
            sx={{ minHeight: 38 }}
          >
            <Tab value="all" label={`All (${counts.total})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="running" label={`In use (${counts.running})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="available" label={`Available (${counts.available})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="unavailable" label={`Unavailable (${counts.unavailable})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
          </Tabs>
        </FilterBar>
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Loading Skeletons */}
      {loading ? (
        <Box sx={gridSx}>
          {[0, 1, 2].map((n) => (
            <Skeleton key={n} variant="rounded" height={220} />
          ))}
        </Box>
      ) : filteredInstruments.length === 0 ? (
        <EmptyState
          icon={<ScienceIcon />}
          title="No ICP instruments found"
          description={
            search || stateFilter !== "all"
              ? "No instruments matched the active search or state filter."
              : "No ICP-OES equipment is configured for this laboratory section."
          }
        />
      ) : (
        <Box sx={gridSx}>
          {filteredInstruments.map((instrument) => (
            <IcpInstrumentCard
              key={instrument.equipmentId}
              instrument={instrument}
              canOperate={canOperate}
              onOpenRun={(eqId, runId) => navigate(ICP_ROUTES.run(eqId, runId))}
              onStartRun={(eqId) => {
                setTargetEquipmentId(eqId);
                setStartDialogOpen(true);
              }}
              onViewHistory={(eqId) => navigate(ICP_ROUTES.history(eqId))}
            />
          ))}
        </Box>
      )}

      {/* Start Run Dialog */}
      {targetEquipmentId !== null && (
        <StartIcpRunDialog
          open={startDialogOpen}
          equipmentId={targetEquipmentId}
          equipmentName={targetInstrument?.name}
          equipmentCode={targetInstrument?.code}
          onClose={() => {
            setStartDialogOpen(false);
            setTargetEquipmentId(null);
          }}
          onRunStarted={(newRun: IcpRunDto) => {
            setStartDialogOpen(false);
            setTargetEquipmentId(null);
            navigate(ICP_ROUTES.run(newRun.equipmentId, newRun.id));
          }}
        />
      )}
    </LabPage>
  );
}
