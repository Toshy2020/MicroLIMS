import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import {
  Box,
  Tabs,
  Tab,
  Button,
  Alert,
  useTheme
} from "@mui/material";
import { WorkspaceSkeleton } from "../../../components/WorkspaceSkeleton";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { ICP_ROUTES } from "../routes";
import { IcpWorkspaceRunHeader } from "./IcpWorkspaceRunHeader";
import { IcpWorkflowStepRail } from "../components/IcpWorkflowStepRail";
import { IcpMethodReadOnlyPanel } from "./IcpMethodReadOnlyPanel";
import { IcpCalibrationPanel } from "../calibration/IcpCalibrationPanel";
import { IcpCcvPanel } from "../ccv/IcpCcvPanel";
import { IcpSampleAssignmentPanel } from "../samples/IcpSampleAssignmentPanel";
import { IcpEvidencePanel } from "../evidence/IcpEvidencePanel";
import { IcpRunHistoryTable } from "../history/IcpRunHistoryTable";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import type { IcpRunDto, IcpRunListItem } from "../types";

function getInitialTab(pathname: string): string {
  if (pathname.endsWith("/calibration")) return "calibration";
  if (pathname.endsWith("/ccv")) return "ccv";
  if (pathname.endsWith("/samples")) return "samples";
  if (pathname.endsWith("/evidence")) return "evidence";
  if (pathname.endsWith("/history")) return "history";
  return "overview";
}

export function IcpRunWorkspace() {
  const theme = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const { instrumentId, runId } = useParams<{ instrumentId: string; runId?: string }>();
  const equipmentId = Number(instrumentId);
  const { permissions, role } = useAuth();
  const canOperate =
    permissions.includes(PERMISSIONS.HPLC_OPERATE) ||
    role === "SystemAdministrator";

  const [activeTab, setActiveTab] = useState<string>(() => getInitialTab(location.pathname));
  const [run, setRun] = useState<IcpRunDto | null>(null);
  const [historyRuns, setHistoryRuns] = useState<IcpRunListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Abandon Dialog
  const [abandonOpen, setAbandonOpen] = useState(false);
  const [abandonReason, setAbandonReason] = useState("");
  const [abandoning, setAbandoning] = useState(false);

  // Complete state
  const [completing, setCompleting] = useState(false);

  useEffect(() => {
    setActiveTab(getInitialTab(location.pathname));
  }, [location.pathname]);

  const loadRunData = useCallback(async () => {
    if (!runId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await IcpWorkspaceService.getRun(Number(runId));
      setRun(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load ICP run data.");
    } finally {
      setLoading(false);
    }
  }, [runId]);

  useEffect(() => {
    if (runId) {
      loadRunData();
    } else {
      setLoading(true);
      IcpWorkspaceService.getInstruments()
        .then((insts) => {
          const matched = insts.find((i) => i.equipmentId === equipmentId);
          if (matched?.activeRun) {
            navigate(ICP_ROUTES.run(equipmentId, matched.activeRun.runId), { replace: true });
          } else {
            IcpWorkspaceService.getRunHistory(equipmentId).then(setHistoryRuns);
            setActiveTab("history");
            setLoading(false);
          }
        })
        .catch(() => setLoading(false));
    }
  }, [equipmentId, runId, navigate, loadRunData]);

  // Load history when on History tab
  useEffect(() => {
    if (activeTab === "history" && equipmentId) {
      IcpWorkspaceService.getRunHistory(equipmentId)
        .then(setHistoryRuns)
        .catch(() => {});
    }
  }, [activeTab, equipmentId]);

  const handleTabChange = (_: React.SyntheticEvent | null, newTab: string) => {
    setActiveTab(newTab);
    if (!run) {
      if (newTab === "history") {
        navigate(ICP_ROUTES.history(equipmentId));
      }
      return;
    }
    const suffix = newTab === "overview" ? "" : `/${newTab}`;
    navigate(`${ICP_ROUTES.run(equipmentId, run.id)}${suffix}`);
  };

  const handleAbandonRun = async () => {
    if (!run || !abandonReason.trim()) return;
    setAbandoning(true);
    try {
      await IcpWorkspaceService.abandonRun(run.id, abandonReason.trim());
      setAbandonOpen(false);
      setAbandonReason("");
      toast.info("Run marked as abandoned.");
      loadRunData();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not abandon run.");
    } finally {
      setAbandoning(false);
    }
  };

  const handleCompleteRun = async () => {
    if (!run) return;
    setCompleting(true);
    setError(null);
    try {
      await IcpWorkspaceService.completeRun(run.id);
      toast.success("Run completed.");
      loadRunData();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not complete run.");
    } finally {
      setCompleting(false);
    }
  };

  if (loading) {
    return <WorkspaceSkeleton />;
  }

  if (!run && activeTab !== "history") {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="warning">Run not found or closed.</Alert>
        <Button onClick={() => navigate(ICP_ROUTES.root)} sx={{ mt: 2 }}>
          Back to Instruments
        </Button>
      </Box>
    );
  }

  const showCcvTab = run?.method?.requireCcv ?? false;

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate(ICP_ROUTES.root)}
        sx={{ mb: 1.5, textTransform: "none", color: "text.secondary" }}
      >
        Instruments
      </Button>

      {run && (
        <IcpWorkspaceRunHeader
          run={run}
          canOperate={canOperate}
          completing={completing}
          onAbandon={() => setAbandonOpen(true)}
          onComplete={handleCompleteRun}
        />
      )}

      {run && (
        <IcpWorkflowStepRail
          run={run}
          activeTabKey={activeTab}
          onSelectTab={(tabKey) => handleTabChange(null, tabKey)}
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
        <Tab label="Overview" value="overview" />
        <Tab label="Calibration" value="calibration" />
        {showCcvTab && <Tab label="CCV" value="ccv" />}
        <Tab label={`Samples (${run?.samples?.length ?? 0})`} value="samples" />
        <Tab label={`Evidence (${run?.evidence?.length ?? 0})`} value="evidence" />
        <Tab label="Run History" value="history" />
      </Tabs>

      {/* Tab: Overview */}
      {activeTab === "overview" && run && (
        <IcpMethodReadOnlyPanel method={run.method} />
      )}

      {/* Tab: Calibration */}
      {activeTab === "calibration" && run && (
        <IcpCalibrationPanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab: CCV */}
      {activeTab === "ccv" && run && showCcvTab && (
        <IcpCcvPanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab: Samples (placeholder in slice 1) */}
      {activeTab === "samples" && run && (
        <IcpSampleAssignmentPanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab: Evidence */}
      {activeTab === "evidence" && run && (
        <IcpEvidencePanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab: Run History */}
      {activeTab === "history" && (
        <IcpRunHistoryTable
          historyRuns={historyRuns}
          equipmentId={equipmentId}
          onSelectRun={(selectedRunId) =>
            navigate(ICP_ROUTES.run(equipmentId, selectedRunId))
          }
        />
      )}

      {/* Abandon Run Dialog */}
      <ReasonDialog
        open={abandonOpen}
        title="Abandon ICP Run"
        label="Reason for Abandoning *"
        placeholder="Explain why this run is being abandoned (e.g. plasma extinguished, nebulizer clog, out of argon)..."
        reason={abandonReason}
        onReasonChange={setAbandonReason}
        onClose={() => {
          setAbandonOpen(false);
          setAbandonReason("");
        }}
        onConfirm={handleAbandonRun}
        confirmText="Abandon Run"
        confirmColor="error"
        loading={abandoning}
        loadingText="Abandoning..."
      />
    </Box>
  );
}
