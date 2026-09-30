import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  Chip,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Alert,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import RefreshIcon from "@mui/icons-material/Refresh";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import WavesOutlinedIcon from "@mui/icons-material/WavesOutlined";
import OpacityOutlinedIcon from "@mui/icons-material/OpacityOutlined";
import ScaleOutlinedIcon from "@mui/icons-material/ScaleOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { toast } from "sonner";
import {
  SolutionMasterService,
  SolutionMaster,
  SolutionType
} from "./services/SolutionMasterService";
import {
  MaterialMasterService,
  MaterialMasterEntry
} from "./services/MaterialMasterService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import { SolutionMasterDialog } from "./solutionMaster/SolutionMasterDialog";
import { ReasonDialog } from "./solutionMaster/ReasonDialog";

export function SolutionMasterPage() {
  const theme = useTheme();
  const { sections, sectionName } = useLaboratorySections();

  const [solutions, setSolutions] = useState<SolutionMaster[]>([]);
  const [materialMasters, setMaterialMasters] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<SolutionMaster | null>(null);

  // Activate / Deactivate Toggle Dialog with Reason
  const [solutionToToggle, setSolutionToToggle] = useState<SolutionMaster | null>(null);
  const [toggleReason, setToggleReason] = useState("");
  const [togglingActive, setTogglingActive] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [solutionData, materialData, mySecData] = await Promise.all([
        SolutionMasterService.getAll(),
        MaterialMasterService.getAll(undefined, true),
        getMySections().catch(() => [])
      ]);
      setSolutions(Array.isArray(solutionData) ? solutionData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load solution masters:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not load solution masters.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleOpenAdd = () => {
    setEditingEntry(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (entry: SolutionMaster) => {
    setEditingEntry(entry);
    setDialogOpen(true);
  };

  const handleOpenToggleActive = (solution: SolutionMaster) => {
    setSolutionToToggle(solution);
    setToggleReason("");
    setToggleError(null);
  };

  const handleConfirmToggleActive = async () => {
    if (!solutionToToggle) return;
    const trimmedReason = toggleReason.trim();
    if (!trimmedReason) {
      setToggleError("A reason is required to activate or deactivate a solution master.");
      return;
    }

    const nextActive = !solutionToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await SolutionMasterService.setActive(
        solutionToToggle.id,
        nextActive,
        trimmedReason,
        solutionToToggle.version
      );
      toast.success(`Solution master "${solutionToToggle.name}" ${nextActive ? "activated" : "deactivated"}.`);
      setSolutionToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not update solution status.");
    } finally {
      setTogglingActive(false);
    }
  };

  const filteredSolutions = useMemo(() => {
    return solutions.filter((sol) => {
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const match =
          sol.name.toLowerCase().includes(q) ||
          (sol.instructions && sol.instructions.toLowerCase().includes(q)) ||
          (sol.storageCondition && sol.storageCondition.toLowerCase().includes(q)) ||
          sol.components.some((c) => c.entryCode.toLowerCase().includes(q) || c.entryName.toLowerCase().includes(q));
        if (!match) return false;
      }
      if (typeFilter !== "ALL" && sol.type !== typeFilter) return false;
      if (statusFilter === "ACTIVE" && !sol.isActive) return false;
      if (statusFilter === "INACTIVE" && sol.isActive) return false;
      if (sectionFilter !== "ALL" && String(sol.sectionId) !== sectionFilter) return false;
      return true;
    });
  }, [solutions, searchQuery, typeFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secId: number, secNameProp?: string) => {
    if (secNameProp) return secNameProp;
    const fromHook = sectionName(secId);
    if (fromHook) return fromHook;
    const foundSec = sections.find((s) => s.sectionId === secId);
    return foundSec?.sectionName ?? `Section #${secId}`;
  };

  const renderTypeChip = (type: SolutionType) => {
    switch (type) {
      case "MobilePhase":
        return <Chip icon={<WavesOutlinedIcon fontSize="small" />} label="Mobile Phase" size="small" color="primary" variant="outlined" sx={{ fontSize: 11, fontWeight: 600 }} />;
      case "Diluent":
        return <Chip icon={<OpacityOutlinedIcon fontSize="small" />} label="Diluent" size="small" color="secondary" variant="outlined" sx={{ fontSize: 11, fontWeight: 600 }} />;
      case "Titrant":
        return <Chip icon={<ScaleOutlinedIcon fontSize="small" />} label="Titrant" size="small" color="info" variant="outlined" sx={{ fontSize: 11, fontWeight: 600 }} />;
      default:
        return <Chip label={type} size="small" variant="outlined" sx={{ fontSize: 11 }} />;
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="Solutions Master"
        subtitle="Manage master recipes for mobile phases, diluents, and volumetric titrants including component stoichiometry, shelf life, pH specifications, and standardization criteria."
      />

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      {/* Filter & Action Toolbar */}
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" }, justifyContent: "space-between", flexWrap: "wrap" }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ flexWrap: "wrap", flex: 1 }}>
            <TextField
              size="small"
              placeholder="Search name, recipe, storage, instructions..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 280 }}
            />
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel id="solution-type-filter-label">Type</InputLabel>
              <Select
                labelId="solution-type-filter-label"
                label="Type"
                value={typeFilter}
                onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
              >
                <MenuItem value="ALL">All Types</MenuItem>
                <MenuItem value="MobilePhase">Mobile Phase</MenuItem>
                <MenuItem value="Diluent">Diluent</MenuItem>
                <MenuItem value="Titrant">Titrant</MenuItem>
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 150 }}>
              <InputLabel id="solution-status-filter-label">Status</InputLabel>
              <Select
                labelId="solution-status-filter-label"
                label="Status"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
              >
                <MenuItem value="ALL">All Statuses</MenuItem>
                <MenuItem value="ACTIVE">Active Only</MenuItem>
                <MenuItem value="INACTIVE">Inactive Only</MenuItem>
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="solution-section-filter-label">Section</InputLabel>
              <Select
                labelId="solution-section-filter-label"
                label="Section"
                value={sectionFilter}
                onChange={(e) => setSectionFilter(e.target.value)}
              >
                <MenuItem value="ALL">All Sections</MenuItem>
                {sections.map((sec) => (
                  <MenuItem key={sec.sectionId} value={String(sec.sectionId)}>
                    {sec.sectionName}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Tooltip title="Refresh">
              <IconButton onClick={loadData} size="small" sx={{ alignSelf: "center" }}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          </Stack>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ fontWeight: 600, textTransform: "none", height: 40 }}
          >
            Add Solution
          </Button>
        </Stack>
      </Paper>

      {/* Solutions Table */}
      <TableContainer component={Paper} elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell sx={{ fontWeight: 600 }}>Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Shelf Life</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Final Volume</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Components</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
                    Loading solution masters...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredSolutions.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <ScienceOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="body1" sx={{ color: "text.secondary", fontWeight: 500 }}>
                    No solution masters found
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.5 }}>
                    {searchQuery || typeFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL"
                      ? "Try adjusting your search or filters."
                      : "Click 'Add Solution' to register your first mobile phase, diluent, or titrant master."}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredSolutions.map((sol) => (
                <TableRow key={sol.id} hover>
                  <TableCell sx={{ fontWeight: 600 }}>
                    <div>{sol.name}</div>
                    <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
                      {sol.phTarget != null && (
                        <Typography variant="caption" color="text.secondary">
                          pH {sol.phTarget}{sol.phTolerance != null ? ` ± ${sol.phTolerance}` : ""}
                        </Typography>
                      )}
                      {sol.type === "Titrant" && sol.nominalStrength != null && (
                        <Typography variant="caption" color="text.secondary">
                          • {sol.nominalStrength} {sol.strengthUnit === "Molar" ? "M" : "N"} ({sol.standardizationMode === "PrimaryStandard" ? "Primary Standard" : "vs Volumetric Solution"})
                        </Typography>
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>{renderTypeChip(sol.type)}</TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {sol.shelfLifeValue} {sol.shelfLifeUnit}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {Number(sol.finalVolumeMl).toLocaleString()} mL
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Tooltip
                      arrow
                      title={
                        <Box sx={{ p: 0.5 }}>
                          <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                            Recipe Breakdown:
                          </Typography>
                          {sol.components.map((c) => (
                            <Typography key={c.materialMasterEntryId} variant="caption" sx={{ display: "block" }}>
                              • {c.entryCode} - {c.entryName}: {c.quantity} {c.unit}
                            </Typography>
                          ))}
                        </Box>
                      }
                    >
                      <Chip
                        label={`${sol.components.length} ${sol.components.length === 1 ? "component" : "components"}`}
                        size="small"
                        variant="outlined"
                        sx={{ fontSize: 11, cursor: "pointer" }}
                      />
                    </Tooltip>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={resolveSectionDisplay(sol.sectionId, sol.sectionName)}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12 }}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip
                      icon={sol.isActive ? <CheckCircleOutlinedIcon fontSize="small" /> : <BlockIcon fontSize="small" />}
                      label={sol.isActive ? "Active" : "Inactive"}
                      size="small"
                      color={sol.isActive ? "success" : "default"}
                      sx={{ fontSize: 11, fontWeight: 600 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title="Edit Solution">
                        <IconButton size="small" onClick={() => handleOpenEdit(sol)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={sol.isActive ? "Deactivate Solution" : "Activate Solution"}>
                        <IconButton
                          size="small"
                          color={sol.isActive ? "error" : "success"}
                          onClick={() => handleOpenToggleActive(sol)}
                        >
                          {sol.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlinedIcon fontSize="small" />}
                        </IconButton>
                      </Tooltip>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Add / Edit Floating Dialog */}
      <SolutionMasterDialog
        open={dialogOpen}
        editingEntry={editingEntry}
        solutions={solutions}
        materialMasters={materialMasters}
        sections={sections}
        mySections={mySections}
        onClose={() => setDialogOpen(false)}
        onSuccess={loadData}
        resolveSectionDisplay={resolveSectionDisplay}
      />

      {/* Activate / Deactivate Confirmation Dialog with Reason */}
      <ReasonDialog
        open={Boolean(solutionToToggle)}
        title={solutionToToggle?.isActive ? "Deactivate Solution Master" : "Activate Solution Master"}
        onClose={() => { if (!togglingActive) setSolutionToToggle(null); }}
        onConfirm={handleConfirmToggleActive}
        confirmText={solutionToToggle?.isActive ? "Deactivate Solution" : "Activate Solution"}
        confirmColor={solutionToToggle?.isActive ? "error" : "success"}
        loading={togglingActive}
        loadingText="Updating..."
        error={toggleError}
        reason={toggleReason}
        onReasonChange={setToggleReason}
        label="Reason *"
        placeholder={solutionToToggle?.isActive ? "State the operational reason for deactivating this solution master..." : "State the operational reason for activating this solution master..."}
        disabled={!toggleReason.trim() || togglingActive}
      >
        <Alert severity={solutionToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
          {solutionToToggle?.isActive
            ? `Are you sure you want to deactivate "${solutionToToggle?.name}"? Inactive solution masters cannot be selected for new preparations.`
            : `Are you sure you want to activate "${solutionToToggle?.name}"?`}
        </Alert>
      </ReasonDialog>
    </Box>
  );
}
