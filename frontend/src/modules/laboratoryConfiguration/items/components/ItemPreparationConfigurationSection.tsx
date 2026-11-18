import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, Paper, Select, Stack, TextField, Typography, MenuItem
} from "@mui/material";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
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
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";

function MappingWarning() {
  const { t } = useTranslation("labConfig");
  return (
    <Alert severity="warning" sx={{ fontSize: 12 }}>
      {t("items.prepConfig.mappingWarning")}
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
  const { t } = useTranslation("labConfig");
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
      .catch(() => setMessage({ text: t("items.prepConfig.loadError"), ok: false }))
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
      setMessage({ text: t("items.prepConfig.savedPending"), ok: true });
      onChanged?.();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? t("items.prepConfig.saveError"), ok: false });
    } finally {
      setSaving(false);
    }
  };

  const approve = async () => {
    setSaving(true);
    setMessage(null);
    try {
      setConfig(await ItemPreparationConfigurationService.approve(itemId));
      setMessage({ text: t("items.prepConfig.approvedMessage"), ok: true });
      onChanged?.();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? t("items.prepConfig.approveError"), ok: false });
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
          <Trans
            i18nKey="items.prepConfig.emptyInfo"
            ns="labConfig"
            values={{ name: itemName }}
            components={{ bold: <strong /> }}
          />
        </Alert>
      )}

      {config && !editing && (
        <>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Chip
              size="small"
              label={config.approvalStatus === "Approved" ? t("items.prepConfig.statusApproved") : t("items.prepConfig.statusPending")}
              color={config.approvalStatus === "Approved" ? "success" : "warning"}
            />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {config.approvalStatus === "Approved" && config.approvedAt
                ? t("items.prepConfig.approvedBy", {
                    name: config.approvedByName ?? t("items.prepConfig.userFallback", { id: config.approvedByUserId }),
                    date: formatDateTimeWithTimeZone(config.approvedAt),
                  })
                : t("items.prepConfig.createdBy", {
                    name: config.createdByName ?? t("items.prepConfig.userFallback", { id: config.createdByUserId }),
                    date: formatDateTimeWithTimeZone(config.createdAt),
                  })}
            </Typography>
          </Box>

          {config.needsMaterialMapping && <MappingWarning />}

          <PreparationStepsSummary config={config} />

          {config.approvalStatus === "PendingReview" && (
            <Alert severity="warning" sx={{ fontSize: 12 }}>
              {t("items.prepConfig.pendingWarning")}
            </Alert>
          )}
        </>
      )}

      {editing && (
        <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
            <TextField label={t("items.prepConfig.sampleAmountLabel")} value={form.amount ?? ""} onChange={(e) => setField("amount", e.target.value)} />
            <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)} inputProps={{ "aria-label": t("items.prepConfig.techniqueAriaLabel") }}>
              <MenuItem value="PourPlate">{t("items.prepConfig.pourPlate")}</MenuItem>
              <MenuItem value="Filtration">{t("items.prepConfig.filtration")}</MenuItem>
            </Select>
          </Box>

          {form.technique === "Filtration" && (
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
              <TextField label={t("items.prepConfig.filtrationVolumeLabel")} value={form.filtrationVolume ?? ""} onChange={(e) => setField("filtrationVolume", e.target.value)} />
              <TextField label={t("items.prepConfig.washingVolumeLabel")} value={form.washingVolume ?? ""} onChange={(e) => setField("washingVolume", e.target.value)} />
            </Box>
          )}

          {config?.needsMaterialMapping && <Box sx={{ mt: 2 }}><MappingWarning /></Box>}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mt: 2 }}>
            <PreparationMaterialPicker
              label={t("items.prepConfig.diluentLabel")} value={diluentSel} onChange={setDiluentSel}
              currentName={config?.diluent}
            />
            <PreparationMaterialPicker
              label={t("items.prepConfig.neutralizerLabel")} value={neutralizerSel} onChange={setNeutralizerSel}
              allowContainedInMedium currentName={config?.neutralizer}
            />
          </Box>
        </Paper>
      )}

      {canManage && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
          {editing ? (
            <>
              <Button onClick={() => { setEditing(false); setMessage(null); }} disabled={saving}>{t("items.prepConfig.cancel")}</Button>
              <Button variant="contained" onClick={save} disabled={saving || !isSelectionComplete(diluentSel) || !isSelectionComplete(neutralizerSel)}>
                {saving ? t("items.prepConfig.saving") : t("items.prepConfig.save")}
              </Button>
            </>
          ) : (
            <>
              {config?.approvalStatus === "PendingReview" && (
                <Button variant="contained" color="success" onClick={approve} disabled={saving}>
                  {t("items.prepConfig.approve")}
                </Button>
              )}
              <Button variant={config ? "outlined" : "contained"} onClick={beginEdit} disabled={saving}>
                {config ? t("items.prepConfig.edit") : t("items.prepConfig.configure")}
              </Button>
            </>
          )}
        </Box>
      )}
    </Stack>
  );
}
