import { useEffect, useState } from "react";
import {
  Paper,
  Box,
  TextField,
  Select,
  MenuItem,
  Button,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Alert,
  IconButton,
  Collapse,
  Typography,
  Chip,
  Divider,
  Tooltip,
  Stack
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import EditIcon from "@mui/icons-material/Edit";
import AddIcon from "@mui/icons-material/Add";
import KeyIcon from "@mui/icons-material/Key";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { OrganismPicker } from "../../../components/OrganismPicker";
import { MediaProductPicker } from "../../../components/MediaProductPicker";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { OrganismOption } from "../../../hooks/useOrganisms";
import {
  masterDataOptions,
  evaluationTypeLabel,
  MediaProductOption
} from "../../../services/masterDataOptions";
import { tableHeadSx } from "../../../theme";

const EVALUATION_TYPES = [
  { value: "GrowthPromotion", label: "Growth Promotion" },
  { value: "IndicationInhibition", label: "Indication / Inhibition" },
  { value: "EnrichmentCharacteristics", label: "Enrichment Characteristics" }
];

const CHALLENGE_ROLES = ["Inhibition", "Indication"];

interface ChallengeItem {
  id: number;
  mediaConfigurationId: number;
  organismId: number;
  challengeRole?: string | null;
  expectedDescription?: string | null;
  initialInoculum?: string | null;
  organism?: {
    id: number;
    scientificName: string;
    atccNumber?: string | null;
    commonName?: string | null;
  } | null;
}

interface MediaConfigurationItem {
  id: number;
  name: string;
  mediaProductId: number;
  mediaProductCode?: string | null;
  evaluationType: string;
  incubationMinHours: number;
  incubationMaxHours: number;
  temperatureMin: number;
  temperatureMax: number;
  recoveryPercentMin?: number | null;
  recoveryPercentMax?: number | null;
  challenges: ChallengeItem[];
}

interface StagedChallenge {
  organismId: number;
  organismName?: string;
  atccNumber?: string;
  challengeRole?: string | null;
  expectedDescription?: string | null;
  initialInoculum?: string | null;
}

// Growth Promotion (and any other role-less evaluation type) has no
// organism-specific direction to reason about, so a plain "10^2" is a
// reasonable starting point the analyst can override. Indication/Inhibition
// rows start blank - the right threshold depends on lab/organism judgment,
// not a single universal constant.
const defaultInitialInoculum = (evalType: string) => (evalType === "IndicationInhibition" ? "" : "10^2");

const getErrorMessage = (err: unknown, fallback: string) =>
  (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

export function MediaConfigurationPage() {
  const { role } = useAuth();
  const isManager = role === "SectionHead" || role === "SystemAdministrator";
  const isSectionHead = role === "SectionHead";

  const [configurations, setConfigurations] = useState<MediaConfigurationItem[]>([]);
  const [mediaProducts, setMediaProducts] = useState<MediaProductOption[]>([]);
  const [organisms, setOrganisms] = useState<OrganismOption[]>([]);
  const [expandedRows, setExpandedRows] = useState<Record<number, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [pendingDelete, setPendingDelete] = useState<MediaConfigurationItem | null>(null);

  // Form State
  const [editingId, setEditingId] = useState<number | null>(null);
  const [mediaProductId, setMediaProductId] = useState<number | null>(null);
  const [evaluationType, setEvaluationType] = useState("GrowthPromotion");
  const [incubationMinHours, setIncubationMinHours] = useState<number | "">("");
  const [incubationMaxHours, setIncubationMaxHours] = useState<number | "">("");
  const [temperatureMin, setTemperatureMin] = useState<number | "">("");
  const [temperatureMax, setTemperatureMax] = useState<number | "">("");
  const [recoveryPercentMin, setRecoveryPercentMin] = useState<number | "">("");
  const [recoveryPercentMax, setRecoveryPercentMax] = useState<number | "">("");

  // Staged Challenge Input State
  const [stagedChallenges, setStagedChallenges] = useState<StagedChallenge[]>([]);
  const [selectedOrganismId, setSelectedOrganismId] = useState<number | null>(null);
  const [challengeRole, setChallengeRole] = useState<string>("");
  const [expectedDescription, setExpectedDescription] = useState<string>("");
  const [initialInoculum, setInitialInoculum] = useState<string>(defaultInitialInoculum("GrowthPromotion"));

  // Media Products dialog states
  const [addProductOpen, setAddProductOpen] = useState(false);
  const [newProductName, setNewProductName] = useState("");
  const [newProductCode, setNewProductCode] = useState("");
  const [addProductError, setAddProductError] = useState<string | null>(null);
  const [addingProduct, setAddingProduct] = useState(false);

  const [renameProduct, setRenameProduct] = useState<MediaProductOption | null>(null);
  const [renameName, setRenameName] = useState("");
  const [renameError, setRenameError] = useState<string | null>(null);
  const [renaming, setRenaming] = useState(false);

  const [pendingDeleteProduct, setPendingDeleteProduct] = useState<MediaProductOption | null>(null);

  const [changeCodeProduct, setChangeCodeProduct] = useState<MediaProductOption | null>(null);
  const [newCode, setNewCode] = useState("");
  const [changeCodeReason, setChangeCodeReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  const loadData = async () => {
    try {
      const [configs, products, orgList] = await Promise.all([
        masterDataOptions.getMediaConfigurations(),
        masterDataOptions.getMediaProducts(),
        masterDataOptions.getOrganisms()
      ]);
      setConfigurations(configs);
      setMediaProducts(products);
      setOrganisms(orgList);
    } catch (err: unknown) {
      setMessage({ text: getErrorMessage(err, "Failed to load media configuration data."), ok: false });
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const toggleRow = (id: number) => {
    setExpandedRows((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const handleAddChallenge = () => {
    if (!selectedOrganismId) return;

    const org = organisms.find((o) => o.id === selectedOrganismId);
    const newChallenge: StagedChallenge = {
      organismId: selectedOrganismId,
      organismName: org?.scientificName ?? `Organism #${selectedOrganismId}`,
      atccNumber: org?.atccNumber ?? undefined,
      challengeRole: evaluationType === "IndicationInhibition" ? (challengeRole || null) : null,
      expectedDescription: (evaluationType === "IndicationInhibition" && challengeRole === "Indication") ? (expectedDescription.trim() || null) : null,
      initialInoculum: initialInoculum.trim() || null
    };

    // Check duplicate
    const duplicate = stagedChallenges.some(
      (c) => c.organismId === newChallenge.organismId && c.challengeRole === newChallenge.challengeRole
    );
    if (duplicate) {
      setMessage({ text: "This organism and role combination is already staged for this configuration.", ok: false });
      return;
    }

    setStagedChallenges((prev) => [...prev, newChallenge]);
    setSelectedOrganismId(null);
    setChallengeRole("");
    setExpectedDescription("");
    setInitialInoculum(defaultInitialInoculum(evaluationType));
    setMessage(null);
  };

  const handleRemoveChallenge = (index: number) => {
    setStagedChallenges((prev) => prev.filter((_, i) => i !== index));
  };

  const resetForm = () => {
    setEditingId(null);
    setMediaProductId(null);
    setEvaluationType("GrowthPromotion");
    setIncubationMinHours("");
    setIncubationMaxHours("");
    setTemperatureMin("");
    setTemperatureMax("");
    setRecoveryPercentMin("");
    setRecoveryPercentMax("");
    setStagedChallenges([]);
    setSelectedOrganismId(null);
    setChallengeRole("");
    setExpectedDescription("");
    setInitialInoculum(defaultInitialInoculum("GrowthPromotion"));
  };

  const startEdit = (config: MediaConfigurationItem) => {
    setEditingId(config.id);
    setMediaProductId(config.mediaProductId ?? null);
    setEvaluationType(config.evaluationType ?? "GrowthPromotion");
    setIncubationMinHours(config.incubationMinHours ?? "");
    setIncubationMaxHours(config.incubationMaxHours ?? "");
    setTemperatureMin(config.temperatureMin ?? "");
    setTemperatureMax(config.temperatureMax ?? "");
    setRecoveryPercentMin(config.recoveryPercentMin ?? "");
    setRecoveryPercentMax(config.recoveryPercentMax ?? "");
    setStagedChallenges(
      (config.challenges ?? []).map((c) => ({
        organismId: c.organismId,
        organismName: c.organism?.scientificName ?? `Organism #${c.organismId}`,
        atccNumber: c.organism?.atccNumber ?? undefined,
        challengeRole: c.challengeRole ?? null,
        expectedDescription: c.expectedDescription ?? null,
        initialInoculum: c.initialInoculum ?? null
      }))
    );
    setSelectedOrganismId(null);
    setChallengeRole("");
    setExpectedDescription("");
    setInitialInoculum(defaultInitialInoculum(config.evaluationType ?? "GrowthPromotion"));
    setMessage(null);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const deleteConfiguration = async (config: MediaConfigurationItem) => {
    setMessage(null);
    try {
      await masterDataOptions.deleteMediaConfiguration(config.id);
      setPendingDelete(null);
      await loadData();
      setMessage({ text: `Media configuration for "${config.name}" deleted.`, ok: true });
    } catch (e: unknown) {
      setPendingDelete(null);
      setMessage({ text: getErrorMessage(e, "Could not delete this media configuration."), ok: false });
    }
  };

  const handleSave = async () => {
    setMessage(null);

    // Validation
    if (!mediaProductId) {
      setMessage({ text: "Media product is required.", ok: false });
      return;
    }
    if (incubationMinHours === "" || incubationMaxHours === "") {
      setMessage({ text: "Incubation range (min and max hours) is required.", ok: false });
      return;
    }
    if (Number(incubationMinHours) < 0) {
      setMessage({ text: "Incubation min hours cannot be negative.", ok: false });
      return;
    }
    if (Number(incubationMinHours) > Number(incubationMaxHours)) {
      setMessage({ text: "Incubation min hours cannot exceed max hours.", ok: false });
      return;
    }
    if (temperatureMin === "" || temperatureMax === "") {
      setMessage({ text: "Temperature range (min and max °C) is required.", ok: false });
      return;
    }
    if (Number(temperatureMin) > Number(temperatureMax)) {
      setMessage({ text: "Temperature min cannot exceed max.", ok: false });
      return;
    }
    if (evaluationType === "GrowthPromotion") {
      if (recoveryPercentMin !== "" && recoveryPercentMax !== "") {
        if (Number(recoveryPercentMin) > Number(recoveryPercentMax)) {
          setMessage({ text: "Recovery % min cannot exceed max.", ok: false });
          return;
        }
      }
    }

    const payload = {
      mediaProductId,
      evaluationType,
      incubationMinHours: Number(incubationMinHours),
      incubationMaxHours: Number(incubationMaxHours),
      temperatureMin: Number(temperatureMin),
      temperatureMax: Number(temperatureMax),
      recoveryPercentMin: (evaluationType === "GrowthPromotion" && recoveryPercentMin !== "") ? Number(recoveryPercentMin) : null,
      recoveryPercentMax: (evaluationType === "GrowthPromotion" && recoveryPercentMax !== "") ? Number(recoveryPercentMax) : null,
      challenges: stagedChallenges.map((c) => ({
        organismId: c.organismId,
        challengeRole: c.challengeRole ?? null,
        expectedDescription: c.expectedDescription ?? null,
        initialInoculum: c.initialInoculum ?? null
      }))
    };

    try {
      if (editingId) {
        await masterDataOptions.updateMediaConfiguration(editingId, payload);
        setMessage({ text: "Media configuration updated successfully.", ok: true });
      } else {
        await masterDataOptions.createMediaConfiguration(payload);
        setMessage({ text: "Media configuration created successfully.", ok: true });
      }
      resetForm();
      await loadData();
    } catch (err: unknown) {
      setMessage({
        text: getErrorMessage(err, editingId ? "Failed to update media configuration." : "Failed to create media configuration."),
        ok: false
      });
    }
  };

  // Media Products Actions
  const handleAddProduct = async () => {
    if (!newProductName.trim() || !newProductCode.trim()) {
      setAddProductError("Both product name and code are required.");
      return;
    }
    setAddingProduct(true);
    setAddProductError(null);
    try {
      await masterDataOptions.createMediaProduct(newProductName.trim(), newProductCode.trim());
      setAddProductOpen(false);
      setNewProductName("");
      setNewProductCode("");
      await loadData();
      setMessage({ text: `Media product "${newProductName.trim()}" created successfully.`, ok: true });
    } catch (err: unknown) {
      setAddProductError(getErrorMessage(err, "Failed to create media product."));
    } finally {
      setAddingProduct(false);
    }
  };

  const openRenameDialog = (p: MediaProductOption) => {
    setRenameProduct(p);
    setRenameName(p.name);
    setRenameError(null);
  };

  const handleRenameProduct = async () => {
    if (!renameProduct || !renameName.trim()) {
      setRenameError("Product name is required.");
      return;
    }
    setRenaming(true);
    setRenameError(null);
    try {
      await masterDataOptions.renameMediaProduct(renameProduct.id, renameName.trim());
      const oldName = renameProduct.name;
      const updatedName = renameName.trim();
      setRenameProduct(null);
      await loadData();
      setMessage({ text: `Media product "${oldName}" renamed to "${updatedName}".`, ok: true });
    } catch (err: unknown) {
      setRenameError(getErrorMessage(err, "Failed to rename media product."));
    } finally {
      setRenaming(false);
    }
  };

  const handleDeleteProduct = async () => {
    if (!pendingDeleteProduct) return;
    const prod = pendingDeleteProduct;
    setPendingDeleteProduct(null);
    setMessage(null);
    try {
      await masterDataOptions.deleteMediaProduct(prod.id);
      await loadData();
      setMessage({ text: `Media product "${prod.name}" deleted.`, ok: true });
    } catch (err: unknown) {
      setMessage({
        text: getErrorMessage(err, "Could not delete this media product."),
        ok: false
      });
    }
  };

  const openChangeCodeDialog = (p: MediaProductOption) => {
    setChangeCodeProduct(p);
    setNewCode("");
    setChangeCodeReason("");
    setSignatureOpen(false);
  };

  const isAddChallengeValid =
    selectedOrganismId != null &&
    (evaluationType !== "IndicationInhibition" || !!challengeRole) &&
    (challengeRole !== "Indication" || !!expectedDescription.trim());

  const selectedProductName = mediaProducts.find((p) => p.id === mediaProductId)?.name
    ?? (editingId ? configurations.find((c) => c.id === editingId)?.name : "")
    ?? "";

  return (
    <>
      <PageHeader
        title="Media Configurations"
        subtitle="Master configuration profiles, incubation & temperature ranges, evaluation rules, and challenge organisms."
      />

      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2.5 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {/* CREATE / EDIT FORM (Managers only) */}
      {isManager && (
        <>
          <SectionTitle>{editingId ? `Edit Media Configuration: ${selectedProductName || "Selected Profile"}` : "New Media Configuration"}</SectionTitle>
          <Paper sx={{ p: 2.5, mb: 4 }}>
            <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2, mb: 2 }}>
              <MediaProductPicker
                value={mediaProductId}
                onChange={(id, product) => {
                  setMediaProductId(id);
                  if (product && !mediaProducts.some((p) => p.id === product.id)) {
                    loadData();
                  }
                }}
                required
                allowCreate={isManager}
              />

              <Select
                size="small"
                value={evaluationType}
                onChange={(e) => {
                  setEvaluationType(e.target.value);
                  setStagedChallenges([]);
                  setChallengeRole("");
                  setExpectedDescription("");
                  setInitialInoculum(defaultInitialInoculum(e.target.value));
                }}
              >
                {EVALUATION_TYPES.map((t) => (
                  <MenuItem key={t.value} value={t.value}>
                    {t.label}
                  </MenuItem>
                ))}
              </Select>

              <Box sx={{ display: "flex", gap: 1 }}>
                <TextField
                  size="small"
                  type="number"
                  label="Incubation Min (h)"
                  value={incubationMinHours}
                  onChange={(e) => setIncubationMinHours(e.target.value === "" ? "" : Number(e.target.value))}
                  required
                />
                <TextField
                  size="small"
                  type="number"
                  label="Incubation Max (h)"
                  value={incubationMaxHours}
                  onChange={(e) => setIncubationMaxHours(e.target.value === "" ? "" : Number(e.target.value))}
                  required
                />
              </Box>

              <Box sx={{ display: "flex", gap: 1 }}>
                <TextField
                  size="small"
                  type="number"
                  label="Temp Min (°C)"
                  value={temperatureMin}
                  onChange={(e) => setTemperatureMin(e.target.value === "" ? "" : Number(e.target.value))}
                  required
                />
                <TextField
                  size="small"
                  type="number"
                  label="Temp Max (°C)"
                  value={temperatureMax}
                  onChange={(e) => setTemperatureMax(e.target.value === "" ? "" : Number(e.target.value))}
                  required
                />
              </Box>

              {evaluationType === "GrowthPromotion" && (
                <Box sx={{ display: "flex", gap: 1 }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Recovery Min (%)"
                    placeholder="e.g. 70"
                    value={recoveryPercentMin}
                    onChange={(e) => setRecoveryPercentMin(e.target.value === "" ? "" : Number(e.target.value))}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Recovery Max (%)"
                    placeholder="e.g. 200"
                    value={recoveryPercentMax}
                    onChange={(e) => setRecoveryPercentMax(e.target.value === "" ? "" : Number(e.target.value))}
                  />
                </Box>
              )}
            </Box>

            <Divider sx={{ my: 2 }} />

            {/* Staged Challenge Organisms Builder */}
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
              Challenge Organisms (Optional)
            </Typography>

            <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2, alignItems: "center", mb: 2 }}>
              <OrganismPicker value={selectedOrganismId} onChange={(id) => setSelectedOrganismId(id)} />

              {evaluationType === "IndicationInhibition" && (
                <Select
                  size="small"
                  displayEmpty
                  value={challengeRole}
                  onChange={(e) => setChallengeRole(e.target.value)}
                >
                  <MenuItem value=""><em>Select Challenge Role</em></MenuItem>
                  {CHALLENGE_ROLES.map((r) => (
                    <MenuItem key={r} value={r}>{r}</MenuItem>
                  ))}
                </Select>
              )}

              {evaluationType === "IndicationInhibition" && challengeRole === "Indication" && (
                <TextField
                  size="small"
                  label="Expected Colony Description"
                  placeholder="e.g. Pink-red with precipitation"
                  value={expectedDescription}
                  onChange={(e) => setExpectedDescription(e.target.value)}
                />
              )}

              <TextField
                size="small"
                label="Initial Inoculum (CFU)"
                placeholder="e.g. 10^2, ≤100, ≥1000"
                value={initialInoculum}
                onChange={(e) => setInitialInoculum(e.target.value)}
              />

              <Box>
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<AddCircleOutlineIcon />}
                  disabled={!isAddChallengeValid}
                  onClick={handleAddChallenge}
                >
                  Add Organism
                </Button>
              </Box>
            </Box>

            {stagedChallenges.length > 0 && (
              <Paper variant="outlined" sx={{ p: 1, mb: 2, backgroundColor: "background.default" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell>Organism</TableCell>
                      <TableCell>ATCC / Ref</TableCell>
                      <TableCell>Role</TableCell>
                      <TableCell>Expected Description</TableCell>
                      <TableCell>Initial Inoculum</TableCell>
                      <TableCell align="right" />
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {stagedChallenges.map((c, idx) => (
                      <TableRow key={idx}>
                        <TableCell sx={{ fontWeight: 500 }}>{c.organismName}</TableCell>
                        <TableCell>{c.atccNumber ?? "—"}</TableCell>
                        <TableCell>{c.challengeRole ?? "—"}</TableCell>
                        <TableCell>{c.expectedDescription ?? "—"}</TableCell>
                        <TableCell>{c.initialInoculum ?? "—"}</TableCell>
                        <TableCell align="right">
                          <IconButton size="small" color="error" onClick={() => handleRemoveChallenge(idx)}>
                            <DeleteOutlineIcon fontSize="small" />
                          </IconButton>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            )}

            <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 2 }}>
              {editingId ? (
                <Button onClick={resetForm}>Cancel Edit</Button>
              ) : (
                <Button onClick={resetForm}>Reset</Button>
              )}
              <Button variant="contained" onClick={handleSave}>
                {editingId ? "Save Changes" : "Create Media Configuration"}
              </Button>
            </Box>
          </Paper>
        </>
      )}

      {/* EXISTING CONFIGURATIONS LIST */}
      <SectionTitle>{`Existing Configurations (${configurations.length})`}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 4 }}>
        <Table>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell sx={{ width: 40 }} />
              <TableCell>Media Name</TableCell>
              <TableCell>Evaluation Type</TableCell>
              <TableCell>Incubation</TableCell>
              <TableCell>Temperature</TableCell>
              <TableCell>Recovery% Band</TableCell>
              <TableCell>Challenge Organisms</TableCell>
              {isManager && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {configurations.map((m) => {
              const isExpanded = !!expandedRows[m.id];
              const challengeCount = m.challenges?.length ?? 0;

              return (
                <Box component="tbody" key={m.id} sx={{ display: "contents" }}>
                  <TableRow hover sx={{ "& > *": { borderBottom: isExpanded ? "unset" : undefined } }}>
                    <TableCell>
                      {challengeCount > 0 ? (
                        <IconButton size="small" onClick={() => toggleRow(m.id)}>
                          {isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                        </IconButton>
                      ) : null}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>
                      {m.name}
                      {m.mediaProductCode ? (
                        <Typography component="span" variant="body2" sx={{ ml: 1, color: "text.secondary", fontWeight: 400 }}>
                          ({m.mediaProductCode})
                        </Typography>
                      ) : null}
                    </TableCell>
                    <TableCell>
                      <Chip size="small" label={evaluationTypeLabel(m.evaluationType)} variant="outlined" />
                    </TableCell>
                    <TableCell>{m.incubationMinHours}–{m.incubationMaxHours}h</TableCell>
                    <TableCell>{m.temperatureMin}–{m.temperatureMax}°C</TableCell>
                    <TableCell>
                      {m.recoveryPercentMin != null && m.recoveryPercentMax != null
                        ? `${m.recoveryPercentMin}–${m.recoveryPercentMax}%`
                        : "—"}
                    </TableCell>
                    <TableCell>
                      {challengeCount > 0 ? (
                        <Chip
                          size="small"
                          label={`${challengeCount} organism${challengeCount > 1 ? "s" : ""}`}
                          onClick={() => toggleRow(m.id)}
                          sx={{ cursor: "pointer" }}
                        />
                      ) : (
                        <Typography variant="body2" sx={{ color: "text.secondary" }}>None</Typography>
                      )}
                    </TableCell>
                    {isManager && (
                      <TableCell align="right">
                        <Tooltip title="Edit configuration">
                          <IconButton size="small" onClick={() => startEdit(m)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="Delete configuration">
                          <IconButton size="small" color="error" onClick={() => setPendingDelete(m)}>
                            <DeleteOutlineIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>

                  {challengeCount > 0 && (
                    <TableRow>
                      <TableCell style={{ paddingBottom: 0, paddingTop: 0 }} colSpan={isManager ? 8 : 7}>
                        <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                          <Box sx={{ margin: 2, pl: 4 }}>
                            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                              Challenge Organisms for {m.name}
                            </Typography>
                            <Table size="small" sx={{ mb: 2 }}>
                              <TableHead>
                                <TableRow sx={tableHeadSx}>
                                  <TableCell>Scientific Name</TableCell>
                                  <TableCell>ATCC Number</TableCell>
                                  <TableCell>Role</TableCell>
                                  <TableCell>Expected Colony Description</TableCell>
                                  <TableCell>Initial Inoculum</TableCell>
                                </TableRow>
                              </TableHead>
                              <TableBody>
                                {m.challenges.map((c) => (
                                  <TableRow key={c.id}>
                                    <TableCell sx={{ fontWeight: 500 }}>{c.organism?.scientificName ?? `Organism #${c.organismId}`}</TableCell>
                                    <TableCell>{c.organism?.atccNumber ?? "—"}</TableCell>
                                    <TableCell>{c.challengeRole ?? "—"}</TableCell>
                                    <TableCell>{c.expectedDescription ?? "—"}</TableCell>
                                    <TableCell>{c.initialInoculum ?? "—"}</TableCell>
                                  </TableRow>
                                ))}
                              </TableBody>
                            </Table>
                          </Box>
                        </Collapse>
                      </TableCell>
                    </TableRow>
                  )}
                </Box>
              );
            })}
            {configurations.length === 0 && (
              <TableRow>
                <TableCell colSpan={isManager ? 8 : 7} align="center" sx={{ py: 3, color: "text.secondary" }}>
                  No media configurations found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Paper>

      {/* MEDIA PRODUCTS SECTION */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 4, mb: 1.5 }}>
        <SectionTitle>{`Media Products (${mediaProducts.length})`}</SectionTitle>
        {isManager && (
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={() => {
              setAddProductOpen(true);
              setNewProductName("");
              setNewProductCode("");
              setAddProductError(null);
            }}
          >
            Add Media Product
          </Button>
        )}
      </Box>
      <Paper sx={{ p: 2.5, mb: 4 }}>
        <Table>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Configurations</TableCell>
              <TableCell>Batches</TableCell>
              {isManager && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {mediaProducts.map((p) => (
              <TableRow key={p.id} hover>
                <TableCell sx={{ fontWeight: 600 }}>{p.code}</TableCell>
                <TableCell>{p.name}</TableCell>
                <TableCell>{p.configurationCount}</TableCell>
                <TableCell>{p.batchCount}</TableCell>
                {isManager && (
                  <TableCell align="right">
                    <Tooltip title="Rename product">
                      <IconButton size="small" onClick={() => openRenameDialog(p)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    {isSectionHead && (
                      <Tooltip title="Change code (Section Head)">
                        <IconButton size="small" color="primary" onClick={() => openChangeCodeDialog(p)}>
                          <KeyIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    <Tooltip title="Delete product">
                      <IconButton size="small" color="error" onClick={() => setPendingDeleteProduct(p)}>
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                )}
              </TableRow>
            ))}
            {mediaProducts.length === 0 && (
              <TableRow>
                <TableCell colSpan={isManager ? 5 : 4} align="center" sx={{ py: 3, color: "text.secondary" }}>
                  No media products found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Paper>

      {/* DIALOGS */}
      {/* Configuration Delete Confirmation */}
      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete media configuration for "${pendingDelete.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteConfiguration(pendingDelete)}
      />

      {/* Media Product Delete Confirmation */}
      <ConfirmationDialog
        open={pendingDeleteProduct != null}
        message={
          pendingDeleteProduct
            ? `Delete media product "${pendingDeleteProduct.name}" (${pendingDeleteProduct.code})? This cannot be undone.`
            : ""
        }
        onCancel={() => setPendingDeleteProduct(null)}
        onConfirm={handleDeleteProduct}
      />

      {/* Add Media Product Dialog */}
      <FloatingDialog
        open={addProductOpen}
        title="Add Media Product"
        onClose={() => setAddProductOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setAddProductOpen(false)} disabled={addingProduct}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleAddProduct}
              disabled={!newProductName.trim() || !newProductCode.trim() || addingProduct}
            >
              {addingProduct ? "Adding..." : "Add Product"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          {addProductError && <Alert severity="error">{addProductError}</Alert>}
          <TextField
            label="Product Name"
            required
            value={newProductName}
            onChange={(e) => setNewProductName(e.target.value)}
            placeholder="e.g. Tryptic Soy Agar"
            autoFocus
            size="small"
          />
          <TextField
            label="Product Code"
            required
            value={newProductCode}
            onChange={(e) => setNewProductCode(e.target.value)}
            placeholder="e.g. TSA"
            helperText="2-10 characters (letters, digits, dot, hyphen)"
            size="small"
            onKeyDown={(e) => {
              if (e.key === "Enter" && newProductName.trim() && newProductCode.trim() && !addingProduct) {
                handleAddProduct();
              }
            }}
          />
        </Stack>
      </FloatingDialog>

      {/* Rename Media Product Dialog */}
      <FloatingDialog
        open={renameProduct != null}
        title={`Rename Media Product: ${renameProduct?.name ?? ""}`}
        onClose={() => setRenameProduct(null)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setRenameProduct(null)} disabled={renaming}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleRenameProduct}
              disabled={!renameName.trim() || renaming}
            >
              {renaming ? "Saving..." : "Save"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          {renameError && <Alert severity="error">{renameError}</Alert>}
          <TextField
            label="Product Name"
            required
            value={renameName}
            onChange={(e) => setRenameName(e.target.value)}
            autoFocus
            size="small"
            onKeyDown={(e) => {
              if (e.key === "Enter" && renameName.trim() && !renaming) {
                handleRenameProduct();
              }
            }}
          />
        </Stack>
      </FloatingDialog>

      {/* Change Code Dialog */}
      <FloatingDialog
        open={changeCodeProduct != null && !signatureOpen}
        title={`Change Code: ${changeCodeProduct?.name ?? ""}`}
        onClose={() => setChangeCodeProduct(null)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setChangeCodeProduct(null)}>Cancel</Button>
            <Button
              variant="contained"
              disabled={!newCode.trim() || !changeCodeReason.trim()}
              onClick={() => setSignatureOpen(true)}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Current Code"
            value={changeCodeProduct?.code ?? ""}
            slotProps={{ input: { readOnly: true } }}
            size="small"
          />
          <TextField
            label="New Code"
            required
            value={newCode}
            onChange={(e) => setNewCode(e.target.value)}
            placeholder="e.g. TSA2"
            helperText="2-10 characters (alphanumeric, '.', or '-'). Backend will validate."
            size="small"
            autoFocus
          />
          <TextField
            label="Reason"
            required
            multiline
            rows={3}
            value={changeCodeReason}
            onChange={(e) => setChangeCodeReason(e.target.value)}
            placeholder="Enter the justification for changing this product code..."
            size="small"
          />
        </Stack>
      </FloatingDialog>

      {/* Electronic Signature Dialog for Change Code */}
      {changeCodeProduct && (
        <SignatureDialog
          open={signatureOpen}
          meaningStatement={`I am changing the code of media product "${changeCodeProduct.name}" from ${changeCodeProduct.code} to ${newCode.trim()}. Lots prepared from now on will be numbered ${newCode.trim()}/01/${String(new Date().getFullYear()).slice(-2)} onwards. Reason: ${changeCodeReason.trim()}`}
          onCancel={() => setSignatureOpen(false)}
          onConfirm={async (password: string) => {
            if (!changeCodeProduct) return;
            await masterDataOptions.changeMediaProductCode(
              changeCodeProduct.id,
              newCode.trim(),
              changeCodeReason.trim(),
              password
            );
            const prodName = changeCodeProduct.name;
            const appliedCode = newCode.trim();
            setSignatureOpen(false);
            setChangeCodeProduct(null);
            setNewCode("");
            setChangeCodeReason("");
            await loadData();
            setMessage({
              text: `Code for "${prodName}" changed to "${appliedCode}".`,
              ok: true
            });
          }}
        />
      )}
    </>
  );
}
