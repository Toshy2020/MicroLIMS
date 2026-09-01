import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, MenuItem, Paper, Select, Stack, TextField, Typography
} from "@mui/material";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "../../../testPreparation/services/ItemPreparationConfigurationService";
import { PreparationStepsSummary } from "../../../testPreparation/PreparationStepsSummary";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { useAuth } from "../../../../contexts/AuthContext";

const UNITS = ["ml", "gm", "bottle", "cap", "25cm2"];

interface Props {
  itemId: number;
  itemName: string;
  onChanged?: () => void;
}

// Laboratory Configuration -> Items -> Preparation Configuration.
// One protocol per item; editing an approved one re-opens it for approval.
export function ItemPreparationConfigurationSection({ itemId, itemName, onChanged }: Props) {
  const { role } = useAuth();
  const canManage = role === "SectionHead" || role === "SystemAdministrator";

  const [config, setConfig] = useState<ItemPreparationConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [diluentTypes, setDiluentTypes] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [neutralizers, setNeutralizers] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ technique: "PourPlate", unit: "ml" });

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

  useEffect(() => {
    masterDataOptions.getDiluentTypes().then(setDiluentTypes);
    masterDataOptions.getNeutralizers().then(setNeutralizers);
  }, []);

  const selectedDiluent = diluentTypes.find((d) => d.id === form.diluentTypeId);

  useEffect(() => {
    if (selectedDiluent?.requiresBatchTracking) {
      masterDataOptions.getReleasedMedia(selectedDiluent.materialId).then(setReleasedMedia);
    }
  }, [form.diluentTypeId]);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const beginEdit = () => {
    setForm(config
      ? {
          amount: config.amount, unit: config.unit, technique: config.technique,
          filtrationVolume: config.filtrationVolume ?? "", washingVolume: config.washingVolume ?? "",
          diluentTypeId: config.diluentTypeId, diluentMediaId: config.diluentMediaId ?? "",
          neutralizerId: config.neutralizerId
        }
      : { technique: "PourPlate", unit: "ml" });
    setMessage(null);
    setEditing(true);
  };

  const save = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const saved = await ItemPreparationConfigurationService.save(itemId, {
        amount: Number(form.amount), unit: form.unit, technique: form.technique,
        filtrationVolume: form.filtrationVolume ? Number(form.filtrationVolume) : null,
        washingVolume: form.washingVolume ? Number(form.washingVolume) : null,
        diluentTypeId: Number(form.diluentTypeId),
        diluentMediaId: form.diluentMediaId ? Number(form.diluentMediaId) : null,
        neutralizerId: Number(form.neutralizerId)
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
            <Select value={form.unit} onChange={(e) => setField("unit", e.target.value)}>
              {UNITS.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
            </Select>
            <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)}>
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

          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
            <Select displayEmpty value={form.diluentTypeId ?? ""} onChange={(e) => setField("diluentTypeId", e.target.value)}>
              <MenuItem value=""><em>Diluent</em></MenuItem>
              {diluentTypes.map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
            </Select>
            {selectedDiluent?.requiresBatchTracking && (
              <Select displayEmpty value={form.diluentMediaId ?? ""} onChange={(e) => setField("diluentMediaId", e.target.value)}>
                <MenuItem value=""><em>Released lot (GPT-released only)</em></MenuItem>
                {releasedMedia.map((m) => <MenuItem key={m.id} value={m.id}>{m.lotNumber} — expires {new Date(m.expiryDate).toLocaleDateString()}</MenuItem>)}
              </Select>
            )}
          </Box>

          <Box sx={{ maxWidth: 300, mt: 2 }}>
            <Select displayEmpty fullWidth value={form.neutralizerId ?? ""} onChange={(e) => setField("neutralizerId", e.target.value)}>
              <MenuItem value=""><em>Neutralizer</em></MenuItem>
              {neutralizers.map((n) => <MenuItem key={n.id} value={n.id}>{n.name}</MenuItem>)}
            </Select>
          </Box>
        </Paper>
      )}

      {canManage && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
          {editing ? (
            <>
              <Button onClick={() => { setEditing(false); setMessage(null); }} disabled={saving}>Cancel</Button>
              <Button variant="contained" onClick={save} disabled={saving}>
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
