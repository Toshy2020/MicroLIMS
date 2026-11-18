import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import {
  Box,
  Typography,
  Tabs,
  Tab,
  Button,
  Stack,
  Alert,
  useTheme
} from "@mui/material";
import { WorkspaceSkeleton } from "../../../components/WorkspaceSkeleton";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import {
  HplcMethodService,
  HplcMethodResponse
} from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import { WorkflowStepRail } from "../components/WorkflowStepRail";
import { MethodReadOnlyPanel } from "./MethodReadOnlyPanel";
import { WorkspaceRunHeader } from "./WorkspaceRunHeader";
import { SystemSuitabilityPanel } from "../sst/SystemSuitabilityPanel";
import { SampleAssignmentPanel } from "../samples/SampleAssignmentPanel";
import { ChromatogramEvidencePanel } from "../evidence/ChromatogramEvidencePanel";
import { HplcRunHistoryTable } from "../history/HplcRunHistoryTable";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { monospaceFontFamily } from "../../../theme/palette";
import { RegisterTable, ResultSection } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import type { HplcRunDto, HplcRunListItem, HplcRunMobilePhaseDto } from "../types";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { useTechnique } from "../useTechnique";
import { useBreadcrumb } from "../../../components/breadcrumbContext";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export function HplcInstrumentWorkspace() {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const { technique, label, routes } = useTechnique();
  const { instrumentId, runId } = useParams<{ instrumentId: string; runId?: string }>();
  const equipmentId = Number(instrumentId);
  const { permissions } = useAuth();
  const canOperate = permissions.includes(PERMISSIONS.HPLC_OPERATE);

  const getInitialTab = () => {
    const path = location.pathname;
    if (path.endsWith("/sst")) return 1;
    if (path.endsWith("/samples")) return 2;
    if (path.endsWith("/evidence")) return 3;
    if (path.endsWith("/history")) return 4;
    return 0;
  };

  const [activeTab, setActiveTab] = useState(getInitialTab);
  const [run, setRun] = useState<HplcRunDto | null>(null);
  const [method, setMethod] = useState<HplcMethodResponse | null>(null);
  const [historyRuns, setHistoryRuns] = useState<HplcRunListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const mobilePhaseColumns = useMemo<RegisterColumn<HplcRunMobilePhaseDto>[]>(
    () => [
      { key: "channel", label: t("run.mobilePhases.channel"), render: (mp) => <strong>{t("run.mobilePhases.channelValue", { channel: mp.channel })}</strong>, sortable: true },
      { key: "solutionMasterName", label: t("run.mobilePhases.solution"), sortable: true },
      {
        key: "solutionPreparationCode",
        label: t("run.mobilePhases.preparationCode"),
        render: (mp) => (
          <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
            {mp.solutionPreparationCode ? <Ltr>{mp.solutionPreparationCode}</Ltr> : "—"}
          </span>
        )
      },
      {
        key: "expiresAt",
        label: t("run.mobilePhases.expiryDate"),
        render: (mp) => (mp.expiresAt ? <Ltr>{formatDateTimeWithTimeZone(mp.expiresAt)}</Ltr> : "—"),
        sortable: true,
        sortValue: (mp) => mp.expiresAt ?? null
      }
    ],
    [t]
  );

  // The last crumb names the run (its reference), or the instrument while no run is open.
  const [instrumentLabel, setInstrumentLabel] = useState<string | null>(null);
  useBreadcrumb(runId ? (error ? t("run.breadcrumbUnavailable") : run?.code ?? null) : instrumentLabel);

  // Abandon Dialog
  const [abandonOpen, setAbandonOpen] = useState(false);
  const [abandonReason, setAbandonReason] = useState("");
  const [abandoning, setAbandoning] = useState(false);

  // Complete Dialog
  const [completing, setCompleting] = useState(false);

  useEffect(() => {
    setActiveTab(getInitialTab());
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname]);

  const loadRunData = useCallback(async () => {
    if (!runId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await HplcWorkspaceService.getRun(Number(runId));
      setRun(data);

      if (data.hplcMethodId) {
        try {
          const m = await HplcMethodService.getById(data.hplcMethodId);
          setMethod(m);
        } catch {
          // Method snapshot is optional
        }
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("run.loadError"));
    } finally {
      setLoading(false);
    }
  }, [runId, t]);

  useEffect(() => {
    if (runId) {
      loadRunData();
    } else {
      setLoading(true);
      HplcWorkspaceService.getInstruments(technique)
        .then((insts) => {
          const matched = insts.find((i) => i.equipmentId === equipmentId);
          if (matched?.activeRun) {
            navigate(routes.run(equipmentId, matched.activeRun.runId), { replace: true });
          } else {
            setInstrumentLabel(matched ? `${matched.code} ${matched.name}` : t("run.instrumentNotFound"));
            HplcWorkspaceService.getRunHistory(equipmentId).then(setHistoryRuns);
            setActiveTab(4);
            setLoading(false);
          }
        })
        .catch(() => {
          setInstrumentLabel(t("run.instrumentUnavailable"));
          setLoading(false);
        });
    }
  }, [equipmentId, runId, navigate, loadRunData, technique, routes, t]);

  // A failed load showed the History tab as if this instrument had no runs.
  const [historyLoadFailed, setHistoryLoadFailed] = useState(false);
  const [historyReloadKey, setHistoryReloadKey] = useState(0);

  useEffect(() => {
    if (activeTab === 4 && equipmentId) {
      setHistoryLoadFailed(false);
      HplcWorkspaceService.getRunHistory(equipmentId).then(setHistoryRuns).catch(() => setHistoryLoadFailed(true));
    }
  }, [activeTab, equipmentId, historyReloadKey]);

  const handleTabChange = (_: React.SyntheticEvent | null, newTab: number) => {
    setActiveTab(newTab);
    if (!run) return;
    const tabPaths = ["", "/sst", "/samples", "/evidence", "/history"];
    const suffix = tabPaths[newTab] ?? "";
    navigate(`${routes.run(equipmentId, run.id)}${suffix}`);
  };

  const handleAbandonRun = async () => {
    if (!run || !abandonReason.trim()) return;
    setAbandoning(true);
    try {
      await HplcWorkspaceService.abandonRun(run.id, abandonReason.trim());
      setAbandonOpen(false);
      toast.info(t("run.abandonedToast"));
      loadRunData();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("run.abandonError"));
    } finally {
      setAbandoning(false);
    }
  };

  const handleCompleteRun = async () => {
    if (!run) return;
    setCompleting(true);
    try {
      await HplcWorkspaceService.completeRun(run.id);
      toast.success(t("run.completedToast"));
      loadRunData();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("run.completeError"));
    } finally {
      setCompleting(false);
    }
  };

  if (loading) {
    return <WorkspaceSkeleton />;
  }

  if (!run && activeTab !== 4) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="warning">{t("run.notFoundAlert")}</Alert>
        <Button onClick={() => navigate(routes.root)} sx={{ mt: 2 }}>{t("run.backToInstruments")}</Button>
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate(routes.root)}
        sx={{ mb: 1.5, textTransform: "none", color: "text.secondary" }}
      >
        {t("run.instruments")}
      </Button>

      {run && (
        <WorkspaceRunHeader
          run={run}
          canOperate={canOperate}
          completing={completing}
          onAbandon={() => setAbandonOpen(true)}
          onComplete={handleCompleteRun}
        />
      )}

      {run && (
        <WorkflowStepRail
          run={run}
          activeTab={activeTab}
          onTabChange={(tabIdx) => handleTabChange(null, tabIdx)}
        />
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Tabs
        value={activeTab}
        onChange={handleTabChange}
        sx={{
          mb: 3,
          borderBottom: `1px solid ${theme.palette.divider}`,
          "& .MuiTab-root": { textTransform: "none", fontWeight: 600, fontSize: 14 }
        }}
      >
        <Tab label={t("run.tabs.overview")} />
        <Tab label={t("run.tabs.sst")} />
        <Tab label={t("run.tabs.samples", { count: run?.samples?.length ?? 0 })} />
        <Tab label={t("run.tabs.evidence", { count: run?.evidence?.length ?? 0 })} />
        <Tab label={t("run.tabs.history")} />
      </Tabs>

      {/* Tab 0: Run setup */}
      {activeTab === 0 && run && (
        <Stack spacing={2}>
          <ResultSection step={1} title={t("run.sections.runSetup")}>
            <Stack spacing={2}>
              {technique !== "Gc" && (
                <>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {t("run.sections.assignedMobilePhases")}
                  </Typography>
                  <RegisterTable
                    columns={mobilePhaseColumns}
                    rows={run.mobilePhases}
                    getRowId={(mp) => mp.id}
                    empty={{ title: t("run.sections.noMobilePhases") }}
                  />
                </>
              )}
              {method && <MethodReadOnlyPanel method={method} />}
            </Stack>
          </ResultSection>
        </Stack>
      )}

      {/* Tab 1: System Suitability */}
      {activeTab === 1 && run && (
        <ResultSection step={2} title={t("run.sections.sst")} status={run.sst?.status}>
          <SystemSuitabilityPanel
            run={run}
            method={method}
            canOperate={canOperate}
            onRunUpdated={loadRunData}
          />
        </ResultSection>
      )}

      {/* Tab 2: Samples */}
      {activeTab === 2 && run && (
        <ResultSection step={3} title={t("run.sections.sampleAssignment")}>
          <SampleAssignmentPanel
            run={run}
            canOperate={canOperate}
            onRunUpdated={loadRunData}
          />
        </ResultSection>
      )}

      {/* Tab 3: Evidence */}
      {activeTab === 3 && run && (
        <ResultSection step={4} title={t("run.sections.testingEvidence")}>
          <ChromatogramEvidencePanel
            run={run}
            canOperate={canOperate}
            onRunUpdated={loadRunData}
          />
        </ResultSection>
      )}

      {/* Tab 4: History */}
      {activeTab === 4 && historyLoadFailed && (
        <LoadErrorAlert
          message={t("run.historyLoadError")}
          onRetry={() => setHistoryReloadKey((k) => k + 1)}
        />
      )}
      {activeTab === 4 && !historyLoadFailed && (
        <HplcRunHistoryTable
          historyRuns={historyRuns}
          equipmentId={equipmentId}
          technique={technique}
          onSelectRun={(selectedRunId) => navigate(routes.run(equipmentId, selectedRunId))}
        />
      )}

      {/* Abandon Run Dialog */}
      <ReasonDialog
        open={abandonOpen}
        title={t("run.abandonDialog.title", { label })}
        onClose={() => setAbandonOpen(false)}
        onConfirm={handleAbandonRun}
        confirmText={t("run.abandonDialog.confirmText")}
        confirmColor="error"
        loading={abandoning}
        loadingText={t("run.abandonDialog.loadingText")}
        reason={abandonReason}
        onReasonChange={setAbandonReason}
        label={t("run.abandonDialog.label")}
        placeholder={t("run.abandonDialog.placeholder")}
      />
    </Box>
  );
}
