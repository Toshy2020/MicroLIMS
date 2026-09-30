import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import {
  Box,
  Typography,
  Tabs,
  Tab,
  Button,
  Stack,
  Alert,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  useTheme
} from "@mui/material";
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
import { tableHeadSx } from "../../../theme";
import type { HplcRunDto, HplcRunListItem } from "../types";

export function HplcInstrumentWorkspace() {
  const theme = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const { instrumentId, runId } = useParams<{ instrumentId: string; runId?: string }>();
  const equipmentId = Number(instrumentId);
  const { permissions, role } = useAuth();
  const canOperate =
    permissions.includes(PERMISSIONS.HPLC_OPERATE) ||
    role === "SystemAdministrator";

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
      setError(e.response?.data?.message ?? e.message ?? "Could not load run data.");
    } finally {
      setLoading(false);
    }
  }, [runId]);

  useEffect(() => {
    if (runId) {
      loadRunData();
    } else {
      setLoading(true);
      HplcWorkspaceService.getInstruments()
        .then((insts) => {
          const matched = insts.find((i) => i.equipmentId === equipmentId);
          if (matched?.activeRun) {
            navigate(`/hplc-workspace/${equipmentId}/run/${matched.activeRun.runId}`, { replace: true });
          } else {
            HplcWorkspaceService.getRunHistory(equipmentId).then(setHistoryRuns);
            setActiveTab(4);
            setLoading(false);
          }
        })
        .catch(() => setLoading(false));
    }
  }, [equipmentId, runId, navigate, loadRunData]);

  useEffect(() => {
    if (activeTab === 4 && equipmentId) {
      HplcWorkspaceService.getRunHistory(equipmentId).then(setHistoryRuns).catch(() => {});
    }
  }, [activeTab, equipmentId]);

  const handleTabChange = (_: React.SyntheticEvent | null, newTab: number) => {
    setActiveTab(newTab);
    if (!run) return;
    const tabPaths = ["", "/sst", "/samples", "/evidence", "/history"];
    const suffix = tabPaths[newTab] ?? "";
    navigate(`/hplc-workspace/${equipmentId}/run/${run.id}${suffix}`);
  };

  const handleAbandonRun = async () => {
    if (!run || !abandonReason.trim()) return;
    setAbandoning(true);
    try {
      await HplcWorkspaceService.abandonRun(run.id, abandonReason.trim());
      setAbandonOpen(false);
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
    try {
      await HplcWorkspaceService.completeRun(run.id);
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
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!run && activeTab !== 4) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="warning">Run not found or closed.</Alert>
        <Button onClick={() => navigate("/hplc-workspace")} sx={{ mt: 2 }}>Back to Instruments</Button>
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate("/hplc-workspace")}
        sx={{ mb: 1.5, textTransform: "none", color: "text.secondary" }}
      >
        Instruments
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
        <Tab label="Overview" />
        <Tab label="System Suitability" />
        <Tab label={`Samples (${run?.samples?.length ?? 0})`} />
        <Tab label={`Evidence (${run?.evidence?.length ?? 0})`} />
        <Tab label="Run History" />
      </Tabs>

      {/* Tab 0: Overview */}
      {activeTab === 0 && run && (
        <Stack spacing={3}>
          <Paper elevation={0} sx={{ p: 2.5, borderRadius: 2, border: `1px solid ${theme.palette.divider}` }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
              Assigned Mobile Phases
            </Typography>
            <TableContainer>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>Channel</TableCell>
                    <TableCell>Solution</TableCell>
                    <TableCell>Preparation Code</TableCell>
                    <TableCell>Expiry Date</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {run.mobilePhases.map((mp) => (
                    <TableRow key={mp.id}>
                      <TableCell sx={{ fontWeight: 700 }}>Channel {mp.channel}</TableCell>
                      <TableCell>{mp.solutionMasterName}</TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{mp.solutionPreparationCode ?? "—"}</TableCell>
                      <TableCell>{mp.expiresAt ? new Date(mp.expiresAt).toLocaleDateString() : "—"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>

          {method && <MethodReadOnlyPanel method={method} />}
        </Stack>
      )}

      {/* Tab 1: System Suitability */}
      {activeTab === 1 && run && (
        <SystemSuitabilityPanel
          run={run}
          method={method}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab 2: Samples */}
      {activeTab === 2 && run && (
        <SampleAssignmentPanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab 3: Evidence */}
      {activeTab === 3 && run && (
        <ChromatogramEvidencePanel
          run={run}
          canOperate={canOperate}
          onRunUpdated={loadRunData}
        />
      )}

      {/* Tab 4: History */}
      {activeTab === 4 && (
        <HplcRunHistoryTable
          historyRuns={historyRuns}
          equipmentId={equipmentId}
          onSelectRun={(selectedRunId) => navigate(`/hplc-workspace/${equipmentId}/run/${selectedRunId}`)}
        />
      )}

      {/* Abandon Run Dialog */}
      <ReasonDialog
        open={abandonOpen}
        title="Abandon HPLC Run"
        onClose={() => setAbandonOpen(false)}
        onConfirm={handleAbandonRun}
        confirmText="Abandon Run"
        confirmColor="error"
        loading={abandoning}
        loadingText="Abandoning..."
        reason={abandonReason}
        onReasonChange={setAbandonReason}
        label="Reason for Abandoning"
        placeholder="Explain why this run is being abandoned..."
      />
    </Box>
  );
}
