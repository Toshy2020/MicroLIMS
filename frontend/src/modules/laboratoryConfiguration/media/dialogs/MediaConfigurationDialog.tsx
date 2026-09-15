import { useState, useEffect } from "react";
import {
  Box,
  Button,
  TextField,
  Select,
  MenuItem,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Alert,
  IconButton,
  Typography,
  Divider,
  Stack,
  Paper,
} from "@mui/material";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { OrganismPicker } from "../../../../components/OrganismPicker";
import { masterDataOptions, incubationConditionLabel } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import {
  MediaConfigurationItem,
  MediaIncubationConditionOption,
  MediaProductOption,
  StagedChallenge,
} from "../types/mediaConfigurationTypes";
import { OrganismOption } from "../../../../hooks/useOrganisms";

const EVALUATION_TYPES = [
  { value: "GrowthPromotion", label: "Growth Promotion" },
  { value: "IndicationInhibition", label: "Indication / Inhibition" },
  { value: "EnrichmentCharacteristics", label: "Enrichment Characteristics" },
];

const CHALLENGE_ROLES = ["Inhibition", "Indication"];

const defaultInitialInoculum = (evalType: string) =>
  evalType === "IndicationInhibition" ? "" : "10^2";

interface MediaConfigurationDialogProps {
  open: boolean;
  product: MediaProductOption;
  configToEdit: MediaConfigurationItem | null;
  // The product's own incubation conditions - the configuration picks one.
  conditions: MediaIncubationConditionOption[];
  organisms: OrganismOption[];
  onClose: () => void;
  onSuccess: () => void;
}

export function MediaConfigurationDialog({
  open,
  product,
  configToEdit,
  conditions,
  organisms,
  onClose,
  onSuccess,
}: MediaConfigurationDialogProps) {
  const [evaluationType, setEvaluationType] = useState("GrowthPromotion");
  const [conditionId, setConditionId] = useState<number | "">("");
  const [recoveryPercentMin, setRecoveryPercentMin] = useState<number | "">("");
  const [recoveryPercentMax, setRecoveryPercentMax] = useState<number | "">("");

  // Staged Challenge Organism Builder
  const [stagedChallenges, setStagedChallenges] = useState<StagedChallenge[]>([]);
  const [selectedOrganismId, setSelectedOrganismId] = useState<number | null>(null);
  const [challengeRole, setChallengeRole] = useState<string>("");
  const [expectedDescription, setExpectedDescription] = useState<string>("");
  const [initialInoculum, setInitialInoculum] = useState<string>(
    defaultInitialInoculum("GrowthPromotion")
  );

  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      if (configToEdit) {
        setEvaluationType(configToEdit.evaluationType ?? "GrowthPromotion");
        setConditionId(configToEdit.mediaIncubationConditionId ?? "");
        setRecoveryPercentMin(configToEdit.recoveryPercentMin ?? "");
        setRecoveryPercentMax(configToEdit.recoveryPercentMax ?? "");
        setStagedChallenges(
          (configToEdit.challenges ?? []).map((c) => ({
            organismId: c.organismId,
            organismName: c.organism?.scientificName ?? `Organism #${c.organismId}`,
            atccNumber: c.organism?.atccNumber ?? undefined,
            challengeRole: c.challengeRole ?? null,
            expectedDescription: c.expectedDescription ?? null,
            initialInoculum: c.initialInoculum ?? null,
          }))
        );
        setInitialInoculum(defaultInitialInoculum(configToEdit.evaluationType ?? "GrowthPromotion"));
      } else {
        setEvaluationType("GrowthPromotion");
        setConditionId("");
        setRecoveryPercentMin("");
        setRecoveryPercentMax("");
        setStagedChallenges([]);
        setInitialInoculum(defaultInitialInoculum("GrowthPromotion"));
      }
      setSelectedOrganismId(null);
      setChallengeRole("");
      setExpectedDescription("");
      setError(null);
      setSaving(false);
    }
  }, [open, configToEdit]);

  const handleEvaluationTypeChange = (newType: string) => {
    setEvaluationType(newType);
    setStagedChallenges([]);
    setChallengeRole("");
    setExpectedDescription("");
    setInitialInoculum(defaultInitialInoculum(newType));
    setError(null);
  };

  const isAddChallengeValid =
    selectedOrganismId != null &&
    (evaluationType !== "IndicationInhibition" || !!challengeRole) &&
    (challengeRole !== "Indication" || !!expectedDescription.trim());

  const handleAddChallenge = () => {
    if (!selectedOrganismId) return;

    const org = organisms.find((o) => o.id === selectedOrganismId);
    const newChallenge: StagedChallenge = {
      organismId: selectedOrganismId,
      organismName: org?.scientificName ?? `Organism #${selectedOrganismId}`,
      atccNumber: org?.atccNumber ?? undefined,
      challengeRole: evaluationType === "IndicationInhibition" ? (challengeRole || null) : null,
      expectedDescription:
        evaluationType === "IndicationInhibition" && challengeRole === "Indication"
          ? (expectedDescription.trim() || null)
          : null,
      initialInoculum: initialInoculum.trim() || null,
    };

    const duplicate = stagedChallenges.some(
      (c) => c.organismId === newChallenge.organismId && c.challengeRole === newChallenge.challengeRole
    );
    if (duplicate) {
      setError("This organism and role combination is already staged for this configuration.");
      return;
    }

    setStagedChallenges((prev) => [...prev, newChallenge]);
    setSelectedOrganismId(null);
    setChallengeRole("");
    setExpectedDescription("");
    setInitialInoculum(defaultInitialInoculum(evaluationType));
    setError(null);
  };

  const handleRemoveChallenge = (index: number) => {
    setStagedChallenges((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    setError(null);

    if (conditionId === "") {
      setError("Choose an incubation condition.");
      return;
    }
    if (evaluationType === "GrowthPromotion") {
      if (recoveryPercentMin !== "" && recoveryPercentMax !== "") {
        if (Number(recoveryPercentMin) > Number(recoveryPercentMax)) {
          setError("Recovery % min cannot exceed max.");
          return;
        }
      }
    }

    const payload = {
      mediaProductId: product.id,
      evaluationType,
      mediaIncubationConditionId: Number(conditionId),
      recoveryPercentMin:
        evaluationType === "GrowthPromotion" && recoveryPercentMin !== ""
          ? Number(recoveryPercentMin)
          : null,
      recoveryPercentMax:
        evaluationType === "GrowthPromotion" && recoveryPercentMax !== ""
          ? Number(recoveryPercentMax)
          : null,
      challenges: stagedChallenges.map((c) => ({
        organismId: c.organismId,
        challengeRole: c.challengeRole ?? null,
        expectedDescription: c.expectedDescription ?? null,
        initialInoculum: c.initialInoculum ?? null,
      })),
    };

    setSaving(true);
    try {
      if (configToEdit) {
        await masterDataOptions.updateMediaConfiguration(configToEdit.id, payload);
      } else {
        await masterDataOptions.createMediaConfiguration(payload);
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(
        msg ??
          (configToEdit
            ? "Failed to update media configuration."
            : "Failed to create media configuration.")
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ fontWeight: 700, fontSize: 16 }}
      title={configToEdit ? `Edit configuration - ${product.name}` : `Add configuration - ${product.name}`}
      actions={
        <>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button variant="contained" onClick={handleSave} disabled={saving || conditions.length === 0}>
            {saving ? "Saving..." : configToEdit ? "Save Changes" : "Save Configuration"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 0.5 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {conditions.length === 0 && (
          <Alert severity="info">
            {product.name} has no incubation conditions yet. Add one in the Incubation Conditions tab first.
          </Alert>
        )}

        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
              Evaluation Type
            </Typography>
            <Select
              size="small"
              fullWidth
              value={evaluationType}
              onChange={(e) => handleEvaluationTypeChange(e.target.value)}
            >
              {EVALUATION_TYPES.map((t) => (
                <MenuItem key={t.value} value={t.value}>
                  {t.label}
                </MenuItem>
              ))}
            </Select>
          </Box>

          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
              Incubation Condition
            </Typography>
            <Select<number | "">
              size="small"
              fullWidth
              displayEmpty
              value={conditionId}
              disabled={conditions.length === 0}
              onChange={(e) => setConditionId(e.target.value === "" ? "" : Number(e.target.value))}
            >
              <MenuItem value="">
                <em>Select a condition *</em>
              </MenuItem>
              {conditions.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {incubationConditionLabel(c)}
                </MenuItem>
              ))}
            </Select>
          </Box>

          {evaluationType === "GrowthPromotion" && (
            <Box sx={{ display: "flex", gap: 1, alignItems: "flex-end" }}>
              <TextField
                size="small"
                type="number"
                label="Recovery Min (%)"
                placeholder="e.g. 70"
                value={recoveryPercentMin}
                onChange={(e) => setRecoveryPercentMin(e.target.value === "" ? "" : Number(e.target.value))}
                fullWidth
              />
              <TextField
                size="small"
                type="number"
                label="Recovery Max (%)"
                placeholder="e.g. 200"
                value={recoveryPercentMax}
                onChange={(e) => setRecoveryPercentMax(e.target.value === "" ? "" : Number(e.target.value))}
                fullWidth
              />
            </Box>
          )}
        </Box>

        <Divider sx={{ my: 1 }} />

        {/* Challenge Organisms Sub-form */}
        <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
          Challenge Organisms (Optional)
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2, alignItems: "center" }}>
          <OrganismPicker value={selectedOrganismId} onChange={(id) => setSelectedOrganismId(id)} />

          {evaluationType === "IndicationInhibition" && (
            <Select
              size="small"
              displayEmpty
              value={challengeRole}
              onChange={(e) => setChallengeRole(e.target.value)}
            >
              <MenuItem value="">
                <em>Select Challenge Role *</em>
              </MenuItem>
              {CHALLENGE_ROLES.map((r) => (
                <MenuItem key={r} value={r}>
                  {r}
                </MenuItem>
              ))}
            </Select>
          )}

          {evaluationType === "IndicationInhibition" && challengeRole === "Indication" && (
            <TextField
              size="small"
              label="Expected Colony Description *"
              placeholder="e.g. Pink-red with precipitation"
              value={expectedDescription}
              onChange={(e) => setExpectedDescription(e.target.value)}
              required
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
          <Paper variant="outlined" sx={{ p: 1, backgroundColor: "background.default" }}>
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
      </Stack>
    </FloatingDialog>
  );
}
