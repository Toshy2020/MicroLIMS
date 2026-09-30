import { useState, useEffect, useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Grid,
  TextField,
  InputAdornment,
  Tabs,
  Tab,
  Button,
  Alert,
  CircularProgress,
  Paper,
  Typography,
  Stack,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RefreshIcon from "@mui/icons-material/Refresh";
import ScienceIcon from "@mui/icons-material/Science";
import PlayCircleOutlinedIcon from "@mui/icons-material/PlayCircleOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import BlockIcon from "@mui/icons-material/Block";
import { PageHeader } from "../../../components/PageHeader";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { HplcInstrumentCard } from "./HplcInstrumentCard";
import type { HplcInstrumentDto } from "../types";

export function HplcWorkspacePage() {
  const theme = useTheme();
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
    instruments.forEach((inst) => {
      const s = inst.state.toLowerCase();
      if (s === "running") running++;
      else if (s === "available") available++;
      else if (s === "unavailable") unavailable++;
    });
    return {
      total: instruments.length,
      running,
      available,
      unavailable
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
      const matchesFilter =
        stateFilter === "all" ||
        (stateFilter === "running" && s === "running") ||
        (stateFilter === "available" && s === "available") ||
        (stateFilter === "unavailable" && s === "unavailable");

      return matchesSearch && matchesFilter;
    });
  }, [instruments, search, stateFilter]);

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <PageHeader
        title="HPLC Workspace"
        subtitle="Instrument operations, system suitability, and sample testing workflow"
      >
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
      </PageHeader>

      {/* KPI Metric Summary Row */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 6, sm: 3 }}>
          <Paper
            elevation={0}
            sx={{
              p: 2,
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`,
              display: "flex",
              alignItems: "center",
              gap: 1.5
            }}
          >
            <ScienceIcon color="action" />
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                Total HPLC Instruments
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {counts.total}
              </Typography>
            </Box>
          </Paper>
        </Grid>

        <Grid size={{ xs: 6, sm: 3 }}>
          <Paper
            elevation={0}
            sx={{
              p: 2,
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`,
              display: "flex",
              alignItems: "center",
              gap: 1.5
            }}
          >
            <PlayCircleOutlinedIcon color="info" />
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                Running Runs
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700, color: "info.main" }}>
                {counts.running}
              </Typography>
            </Box>
          </Paper>
        </Grid>

        <Grid size={{ xs: 6, sm: 3 }}>
          <Paper
            elevation={0}
            sx={{
              p: 2,
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`,
              display: "flex",
              alignItems: "center",
              gap: 1.5
            }}
          >
            <CheckCircleOutlinedIcon color="success" />
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                Available
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700, color: "success.main" }}>
                {counts.available}
              </Typography>
            </Box>
          </Paper>
        </Grid>

        <Grid size={{ xs: 6, sm: 3 }}>
          <Paper
            elevation={0}
            sx={{
              p: 2,
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`,
              display: "flex",
              alignItems: "center",
              gap: 1.5
            }}
          >
            <BlockIcon color="error" />
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                Unavailable
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700, color: "error.main" }}>
                {counts.unavailable}
              </Typography>
            </Box>
          </Paper>
        </Grid>
      </Grid>

      {/* Search and Filters */}
      <Paper
        elevation={0}
        sx={{
          p: 2,
          mb: 3,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: { xs: "stretch", sm: "center" },
            justifyContent: "space-between"
          }}
        >
          <Tabs
            value={stateFilter}
            onChange={(_, val) => setStateFilter(val)}
            sx={{ minHeight: 38 }}
          >
            <Tab value="all" label={`All (${counts.total})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="running" label={`Running (${counts.running})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="available" label={`Available (${counts.available})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
            <Tab value="unavailable" label={`Unavailable (${counts.unavailable})`} sx={{ textTransform: "none", minHeight: 38, py: 0.5 }} />
          </Tabs>

          <TextField
            size="small"
            placeholder="Search by code, name, run or method..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
                  </InputAdornment>
                )
              }
            }}
            sx={{ minWidth: { sm: 280 } }}
          />
        </Stack>
      </Paper>

      {/* Error alert */}
      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Loading state */}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
          <CircularProgress />
        </Box>
      ) : filteredInstruments.length === 0 ? (
        <Paper
          elevation={0}
          sx={{
            p: 6,
            textAlign: "center",
            borderRadius: 2,
            border: `1px dashed ${theme.palette.divider}`
          }}
        >
          <ScienceIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
          <Typography variant="h6" sx={{ color: "text.secondary", fontWeight: 600 }}>
            No instruments found
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            {search
              ? "Try adjusting your search criteria."
              : "No HPLC instruments configured for your section."}
          </Typography>
        </Paper>
      ) : (
        <Grid container spacing={2.5}>
          {filteredInstruments.map((inst) => (
            <Grid size={{ xs: 12, sm: 6, md: 4 }} key={inst.equipmentId}>
              <HplcInstrumentCard
                instrument={inst}
                canOperate={canOperate}
                onOpenRun={(eqId, runId) => navigate(`/hplc-workspace/${eqId}/run/${runId}`)}
                onStartRun={(eqId) => navigate(`/hplc-workspace/${eqId}/new-run`)}
                onViewHistory={(eqId) => navigate(`/hplc-workspace/${eqId}/history`)}
              />
            </Grid>
          ))}
        </Grid>
      )}
    </Box>
  );
}
