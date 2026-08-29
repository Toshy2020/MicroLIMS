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
  Autocomplete,
  IconButton,
  Collapse,
  Typography,
  Chip,
  Divider,
  Tooltip
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutline";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditIcon from "@mui/icons-material/Edit";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { OrganismPicker } from "../../../components/OrganismPicker";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { masterDataOptions, evaluationTypeLabel } from "../../../services/masterDataOptions";
import { MaterialService } from "../../inventory/materials/services/MaterialService";

const EVALUATION_TYPES = [
  { value: "GrowthPromotion", label: "Growth Promotion" },
  { value: "IndicationInhibition", label: "Indication / Inhibition" },
  { value: "EnrichmentCharacteristics", label: "Enrichment Characteristics" }
];

const CHALLENGE_ROLES = ["Inhibition", "Indication"];

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

export function MediaConfigurationPage() {
  const [configurations, setConfigurations] = useState<any[]>([]);
  const [materialNames, setMaterialNames] = useState<string[]>([]);
  const [organisms, setOrganisms] = useState<any[]>([]);
  const [expandedRows, setExpandedRows] = useState<Record<number, boolean>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [pendingDelete, setPendingDelete] = useState<any>(null);

  // Form State
  const [editingId, setEditingId] = useState<number | null>(null);
  const [name, setName] = useState("");
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

  const loadData = async () => {
    try {
      const [configs, materials, orgList] = await Promise.all([
        masterDataOptions.getMediaConfigurations(),
        MaterialService.getAll("DehydratedMedia"),
        masterDataOptions.getOrganisms()
      ]);
      setConfigurations(configs);
      setMaterialNames(Array.from(new Set(materials.map((m: any) => m.materialName))));
      setOrganisms(orgList);
    } catch (err: any) {
      setMessage({ text: err?.response?.data?.message ?? "Failed to load media configurations.", ok: false });
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
      atccNumber: org?.atccNumber,
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
    setName("");
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

  const startEdit = (config: any) => {
    setEditingId(config.id);
    setName(config.name ?? "");
    setEvaluationType(config.evaluationType ?? "GrowthPromotion");
    setIncubationMinHours(config.incubationMinHours ?? "");
    setIncubationMaxHours(config.incubationMaxHours ?? "");
    setTemperatureMin(config.temperatureMin ?? "");
    setTemperatureMax(config.temperatureMax ?? "");
    setRecoveryPercentMin(config.recoveryPercentMin ?? "");
    setRecoveryPercentMax(config.recoveryPercentMax ?? "");
    setStagedChallenges(
      (config.challenges ?? []).map((c: any) => ({
        organismId: c.organismId,
        organismName: c.organism?.scientificName ?? `Organism #${c.organismId}`,
        atccNumber: c.organism?.atccNumber,
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

  const deleteConfiguration = async (config: any) => {
    setMessage(null);
    try {
      await masterDataOptions.deleteMediaConfiguration(config.id);
      setPendingDelete(null);
      await loadData();
      setMessage({ text: `Media configuration for "${config.name}" deleted.`, ok: true });
    } catch (e: any) {
      setPendingDelete(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this media configuration.", ok: false });
    }
  };

  const handleSave = async () => {
    setMessage(null);

    // Validation
    if (!name.trim()) {
      setMessage({ text: "Media name is required.", ok: false });
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
      name: name.trim(),
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
        setMessage({ text: `Media configuration for "${payload.name}" updated successfully.`, ok: true });
      } else {
        await masterDataOptions.createMediaConfiguration(payload);
        setMessage({ text: `Media configuration for "${payload.name}" created successfully.`, ok: true });
      }
      resetForm();
      loadData();
    } catch (err: any) {
      setMessage({ text: err?.response?.data?.message ?? (editingId ? "Failed to update media configuration." : "Failed to create media configuration."), ok: false });
    }
  };

  const isAddChallengeValid =
    selectedOrganismId != null &&
    (evaluationType !== "IndicationInhibition" || !!challengeRole) &&
    (challengeRole !== "Indication" || !!expectedDescription.trim());

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

      {/* CREATE / EDIT FORM */}
      <SectionTitle>{editingId ? `Edit Media Configuration: ${name || "Selected Profile"}` : "New Media Configuration"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 4 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2, mb: 2 }}>
          <Autocomplete
            freeSolo
            options={materialNames}
            value={name}
            onChange={(_e, v) => setName(v ?? "")}
            onInputChange={(_e, v) => setName(v)}
            renderInput={(params) => <TextField {...params} label="Media Product Name" placeholder="e.g. Tryptic Soy Agar" size="small" required />}
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
                <TableRow>
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

      {/* LIST TABLE */}
      <SectionTitle>{`Existing Configurations (${configurations.length})`}</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 40 }} />
              <TableCell>Media Name</TableCell>
              <TableCell>Evaluation Type</TableCell>
              <TableCell>Incubation</TableCell>
              <TableCell>Temperature</TableCell>
              <TableCell>Recovery% Band</TableCell>
              <TableCell>Challenge Organisms</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {configurations.map((m) => {
              const isExpanded = !!expandedRows[m.id];
              const challengeCount = m.challenges?.length ?? 0;

              return (
                <>
                  <TableRow key={m.id} hover sx={{ "& > *": { borderBottom: isExpanded ? "unset" : undefined } }}>
                    <TableCell>
                      {challengeCount > 0 ? (
                        <IconButton size="small" onClick={() => toggleRow(m.id)}>
                          {isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                        </IconButton>
                      ) : null}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{m.name}</TableCell>
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
                        <Typography variant="body2" color="text.secondary">None</Typography>
                      )}
                    </TableCell>
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
                  </TableRow>

                  {challengeCount > 0 && (
                    <TableRow key={`${m.id}-detail`}>
                      <TableCell style={{ paddingBottom: 0, paddingTop: 0 }} colSpan={8}>
                        <Collapse in={isExpanded} timeout="auto" unmountOnExit>
                          <Box sx={{ margin: 2, pl: 4 }}>
                            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                              Challenge Organisms for {m.name}
                            </Typography>
                            <Table size="small" sx={{ mb: 2 }}>
                              <TableHead>
                                <TableRow>
                                  <TableCell>Scientific Name</TableCell>
                                  <TableCell>ATCC Number</TableCell>
                                  <TableCell>Role</TableCell>
                                  <TableCell>Expected Colony Description</TableCell>
                                  <TableCell>Initial Inoculum</TableCell>
                                </TableRow>
                              </TableHead>
                              <TableBody>
                                {m.challenges.map((c: any) => (
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
                </>
              );
            })}
          </TableBody>
        </Table>
      </Paper>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete media configuration for "${pendingDelete.name}"? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteConfiguration(pendingDelete)}
      />
    </>
  );
}
