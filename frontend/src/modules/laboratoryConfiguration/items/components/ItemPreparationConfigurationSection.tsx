import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, Paper, Select, Stack, TextField, Typography, MenuItem
} from "@mui/material";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "../../../testPreparation/services/ItemPreparationConfigurationService";
import { PreparationStepsSummary } from "../../../testPreparation/PreparationStepsSummary";
import { PreparationMaterialPicker } from "../../../testPreparation/components/PreparationMaterialPicker";
import {
  EMPTY_SELECTION, isSelectionComplete, selectionFromConfig, selectionPayload,
  type PreparationMaterialSelection
} from "../../../testPreparation/preparationMaterial";
import { useAuth } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";

function MappingWarning() {
  return (
    <Alert severity="warning" sx={{ fontSize: 12 }}>
      Diluent/neutralizer not linked to master data — edit and pick them so the lot used can be recorded.
    </Alert>
  );
}

interface Props {
  itemId: number;
  itemName: string;
  onChanged?: () => void;
}

// Laboratory Configuration -> Items -> Preparation Configuration.
// One protocol per item; editing an approved one re-opens it for approval.
export function ItemPreparationConfigurationSection({ itemId, itemName, onChanged }: Props) {
  const { permissions } = useAuth();
  const canManage = permissions.includes(PERMISSIONS.ITEMS_MANAGE);

  const [config, setConfig] = useState<ItemPreparationConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [form, setForm] = useState<Record<string, any>>({ technique: "PourPlate" });
  const [diluentSel, setDiluentSel] = useState<PreparationMaterialSelection>(EMPTY_SELECTION);
  const [neutralizerSel, setNeutralizerSel] = useState<PreparationMaterialSelection>(EMPTY_SELECTION);

  const load = () => {
    setLoading(true);
    ItemPreparationConfigurationService.get(itemId)
      .then(setConfig)
      .catch(() => setMessage({ text: "Could not load the preparation configuration.", ok: false }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    setEditing(false);
    setMessage(null);
  }, [itemId]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const beginEdit = () => {
    setForm(config
      ? {
          amount: config.amount, technique: config.technique,
          filtrationVolume: config.filtrationVolume ?? "", washingVolume: config.washingVolume ?? ""
        }
      : { technique: "PourPlate" });
    setDiluentSel(config ? selectionFromConfig(config, "diluent") : EMPTY_SELECTION);
    setNeutralizerSel(config ? selectionFromConfig(config, "neutralizer") : EMPTY_SELECTION);
    setMessage(null);
    setEditing(true);
  };

  const save = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const saved = await ItemPreparationConfigurationService.save(itemId, {
        amount: Number(form.amount), technique: form.technique,
        filtrationVolume: form.filtrationVolume ? Number(form.filtrationVolume) : null,
        washingVolume: form.washingVolume ? Number(form.washingVolume) : null,
        ...(selectionPayload("diluent", diluentSel) as any),
        ...(selectionPayload("neutralizer", neutralizerSel) as any)
      });
      setConfig(saved);
      setEditing(false);
      setMessage({ text: "Preparation configuration saved. It is pending approval.", ok: true });
      onChanged?.();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save the configuration.", ok: false });
    } finally {
      setSaving(false);
    }
  };

  const approve = async () => {
    setSaving(true);
    setMessage(null);
    try {
      setConfig(await ItemPreparationConfigurationService.approve(itemId));
      setMessage({ text: "Preparation configuration approved.", ok: true });
      onChanged?.();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not approve the configuration.", ok: false });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}><CircularProgress size={28} /></Box>;
  }

  return (
    <Stack spacing={2}>
      {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}

      {!config && !editing && (
        <Alert severity="info" sx={{ fontSize: 12 }}>
          No preparation configuration set for <strong>{itemName}</strong>. One will be created automatically
          from the first analyst confirmation, or you can configure it now.
        </Alert>
      )}

      {config && !editing && (
        <>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Chip
              size="small"
              label={config.approvalStatus === "Approved" ? "Approved" : "Pending Approval"}
              color={config.approvalStatus === "Approved" ? "success" : "warning"}
            />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {config.approvalStatus === "Approved" && config.approvedAt
                ? `Approved by ${config.approvedByName ?? `User #${config.approvedByUserId}`} on ${new Date(config.approvedAt).toLocaleDateString()}`
                : `Created by ${config.createdByName ?? `User #${config.createdByUserId}`} on ${new Date(config.createdAt).toLocaleDateString()}`}
            </Typography>
          </Box>

          {config.needsMaterialMapping && <MappingWarning />}

          <PreparationStepsSummary config={config} />

          {config.approvalStatus === "PendingReview" && (
            <Alert severity="warning" sx={{ fontSize: 12 }}>
              This configuration is in effect and is already being used for testing. Approving it records your
              review; editing it first replaces the protocol for future samples.
            </Alert>
          )}
        </>
      )}

      {editing && (
        <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
            <TextField label="Sample Amount" value={form.amount ?? ""} onChange={(e) => setField("amount", e.target.value)} />
            <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)} inputProps={{ "aria-label": "Technique" }}>
              <MenuItem value="PourPlate">Pour Plate</MenuItem>
              <MenuItem value="Filtration">Filtration</MenuItem>
            </Select>
          </Box>

          {form.technique === "Filtration" && (
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
              <TextField label="Filtration Volume (ml)" value={form.filtrationVolume ?? ""} onChange={(e) => setField("filtrationVolume", e.target.value)} />
              <TextField label="Washing Volume (ml)" value={form.washingVolume ?? ""} onChange={(e) => setField("washingVolume", e.target.value)} />
            </Box>
          )}

          {config?.needsMaterialMapping && <Box sx={{ mt: 2 }}><MappingWarning /></Box>}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mt: 2 }}>
            <PreparationMaterialPicker
              label="Diluent" value={diluentSel} onChange={setDiluentSel}
              currentName={config?.diluent}
            />
            <PreparationMaterialPicker
              label="Neutralizer" value={neutralizerSel} onChange={setNeutralizerSel}
              allowContainedInMedium currentName={config?.neutralizer}
            />
          </Box>
        </Paper>
      )}

      {canManage && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
          {editing ? (
            <>
              <Button onClick={() => { setEditing(false); setMessage(null); }} disabled={saving}>Cancel</Button>
              <Button variant="contained" onClick={save} disabled={saving || !isSelectionComplete(diluentSel) || !isSelectionComplete(neutralizerSel)}>
                {saving ? "Saving..." : "Save Configuration"}
              </Button>
            </>
          ) : (
            <>
              {config?.approvalStatus === "PendingReview" && (
                <Button variant="contained" color="success" onClick={approve} disabled={saving}>
                  Approve
                </Button>
              )}
              <Button variant={config ? "outlined" : "contained"} onClick={beginEdit} disabled={saving}>
                {config ? "Edit" : "Configure Preparation"}
              </Button>
            </>
          )}
        </Box>
      )}
    </Stack>
  );
}
