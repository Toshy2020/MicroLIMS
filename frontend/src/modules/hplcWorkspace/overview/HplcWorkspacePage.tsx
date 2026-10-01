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
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { HplcInstrumentCard } from "./HplcInstrumentCard";
import type { HplcInstrumentDto } from "../types";

export function HplcWorkspacePage() {
  const navigate = useNavigate();
  const { permissions, role } = useAuth();
  const canOperate =
    permissions.includes(PERMISSIONS.HPLC_OPERATE) ||
    role === "SystemAdministrator";

  const [instruments, setInstruments] = useState<HplcInstrumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [stateFilter, setStateFilter] = useState<string>("all");

  const loadInstruments = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await HplcWorkspaceService.getInstruments();
      setInstruments(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load HPLC instruments.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadInstruments();
  }, [loadInstruments]);

  // Summary counts
  const counts = useMemo(() => {
    let running = 0;
    let available = 0;
    let unavailable = 0;
    let sstPending = 0;
    instruments.forEach((inst) => {
      const s = inst.state.toLowerCase();
      if (s === "running") {
        running++;
        if (inst.activeRun?.sstStatus === "Pending") sstPending++;
      } else if (s === "available") available++;
      else if (s === "unavailable") unavailable++;
    });
    return {
      total: instruments.length,
      running,
      available,
      unavailable,
      sstPending
    };
  }, [instruments]);

  // Filtered instruments
  const filteredInstruments = useMemo(() => {
    return instruments.filter((inst) => {
      const matchesSearch =
        !search.trim() ||
        inst.name.toLowerCase().includes(search.toLowerCase()) ||
        inst.code.toLowerCase().includes(search.toLowerCase()) ||
        inst.activeRun?.code.toLowerCase().includes(search.toLowerCase()) ||
        inst.activeRun?.methodAbbreviation.toLowerCase().includes(search.toLowerCase());

      const s = inst.state.toLowerCase();
      const matchesFilter = stateFilter === "all" || s === stateFilter;

      return matchesSearch && matchesFilter;
    });
  }, [instruments, search, stateFilter]);

  // One grid definition for cards and skeletons so the layout never jumps.
  const gridSx = {
    display: "grid",
    gridTemplateColumns: { xs: "minmax(0, 1fr)", sm: "repeat(2, minmax(0, 1fr))", lg: "repeat(3, minmax(0, 1fr))" },
    gap: 2,
    alignItems: "stretch"
  } as const;

  return (
    <LabPage
      title="HPLC Workspace"
      subtitle="Instrument operations, system suitability, and sample testing workflow"
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
            { label: "SST pending", value: counts.sstPending, tone: counts.sstPending > 0 ? "pending" : undefined },
            { label: "Unavailable", value: counts.unavailable, tone: counts.unavailable > 0 ? "detected" : undefined }
          ]}
        />
      }
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder="Search by code, name, run or method"
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
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={gridSx}>
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} variant="rounded" height={220} />
          ))}
        </Box>
      ) : filteredInstruments.length === 0 ? (
        <EmptyState
          icon={<ScienceIcon />}
          title="No instruments found"
          description={search ? "Try adjusting your search criteria." : "No HPLC instruments configured for your section."}
        />
      ) : (
        <Box sx={gridSx}>
          {filteredInstruments.map((inst) => (
            <HplcInstrumentCard
              key={inst.equipmentId}
              instrument={inst}
              canOperate={canOperate}
              onOpenRun={(eqId, runId) => navigate(`/hplc-workspace/${eqId}/run/${runId}`)}
              onStartRun={(eqId) => navigate(`/hplc-workspace/${eqId}/new-run`)}
              onViewHistory={(eqId) => navigate(`/hplc-workspace/${eqId}/history`)}
            />
          ))}
        </Box>
      )}
    </LabPage>
  );
}
