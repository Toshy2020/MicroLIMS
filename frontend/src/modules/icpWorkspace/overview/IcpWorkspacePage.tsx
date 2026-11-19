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
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { ICP_ROUTES } from "../routes";
import { IcpInstrumentCard } from "./IcpInstrumentCard";
import { StartIcpRunDialog } from "../run/StartIcpRunDialog";
import type { IcpInstrumentDto, IcpRunDto } from "../types";

export function IcpWorkspacePage() {
  const { t } = useTranslation("icpWorkspace");
  const navigate = useNavigate();
  const { permissions } = useAuth();
  const canOperate = permissions.includes(PERMISSIONS.HPLC_OPERATE);

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
      setError(e.response?.data?.message ?? e.message ?? t("overview.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

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
        (inst.activeRun?.code.toLowerCase().includes(search.toLowerCase()) ?? false);

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
      title={t("overview.title")}
      subtitle={t("overview.subtitle")}
      actions={
        <Button
          variant="outlined"
          size="small"
          startIcon={<RefreshIcon />}
          onClick={loadInstruments}
          disabled={loading}
          sx={{ textTransform: "none" }}
        >
          {t("overview.refresh")}
        </Button>
      }
      kpis={
        <KpiStrip
          loading={loading}
          tiles={[
            { label: t("overview.kpi.instruments"), value: counts.total },
            { label: t("overview.kpi.available"), value: counts.available, tone: "notDetected" },
            { label: t("overview.kpi.inUse"), value: counts.running, tone: "info" },
            { label: t("overview.kpi.calPending"), value: counts.calPending, tone: counts.calPending > 0 ? "pending" : undefined },
            { label: t("overview.kpi.unavailable"), value: counts.unavailable, tone: counts.unavailable > 0 ? "detected" : undefined }
          ]}
        />
      }
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder={t("overview.searchPlaceholder")}
          resultCount={filteredInstruments.length}
        >
          <Tabs
            value={stateFilter}
            onChange={(_, val) => setStateFilter(val)}
            sx={{ minHeight: 38 }}
          >
            <Tab value="all" label={t("overview.tabs.all", { count: counts.total })} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="running" label={t("overview.tabs.inUse", { count: counts.running })} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="available" label={t("overview.tabs.available", { count: counts.available })} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="unavailable" label={t("overview.tabs.unavailable", { count: counts.unavailable })} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
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
          title={t("overview.empty.title")}
          description={
            search || stateFilter !== "all"
              ? t("overview.empty.filteredDescription")
              : t("overview.empty.noneConfigured")
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
