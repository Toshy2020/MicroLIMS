import { useEffect, useState } from "react";
import { Box, Select, MenuItem, TextField, Button, Alert } from "@mui/material";
import { Trans } from "react-i18next";
import { SamplePreparationService, type PreparationLotGroup } from "./services/SamplePreparationService";
import { PreparationMaterialPicker } from "./components/PreparationMaterialPicker";
import { PreparationLotPicker } from "./components/PreparationLotPicker";
import {
  EMPTY_SELECTION, isSelectionComplete, selectionPayload,
  type PreparationMaterialSelection
} from "./preparationMaterial";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useAuth } from "../../contexts/AuthContext";
import { i18n, useTranslation } from "../../i18n";
import { serverMessage } from "../../utils/errorMessage";

interface Props {
  sample: {
    sampleId: number;
    category: string;
    itemName?: string | null;
    // Every analyst on the sample's tests - any of them may prepare it.
    assignedAnalystIds?: number[];
    assignedAnalystNames?: string[];
  };
  onSaved: () => void;
}

// The Test Preparation form - shared by the standalone TestPreparationPage
// (pick-a-sample-from-a-dropdown flow) and the "Needs Preparation" dialog
// opened directly from a Testing Workspace card. Once per Sample; renamed
// "Start Testing" since completing it also auto-assigns the preparer as
// analyst on every Waiting test order for this sample.
// The lots of the master just chosen (no configuration exists yet). Null
// until a master is chosen; ContainedInMedium never needs a lot.
function useMasterLots(sel: PreparationMaterialSelection) {
  const { t } = useTranslation("testPreparation");
  const [group, setGroup] = useState<PreparationLotGroup | null>(null);
  const [lotId, setLotId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLotId(null);
    setError(null);
    if (!sel.source || sel.source === "ContainedInMedium" || sel.masterId == null) {
      setGroup(null);
      return;
    }
    let cancelled = false;
    SamplePreparationService.getLotOptionsForMaster(sel.source, sel.masterId)
      .then((g) => { if (!cancelled) setGroup(g); })
      .catch((e: unknown) => {
        if (!cancelled) {
          setGroup(null);
          setError(serverMessage(e) ?? t("manual.loadLotsError"));
        }
      });
    return () => { cancelled = true; };
  }, [sel.source, sel.masterId, t]);

  return { group, lotId, setLotId, error };
}

interface TestPreparationFormState {
  amount?: string;
  technique: string;
  filtrationVolume?: string;
  washingVolume?: string;
}

export function TestPreparationForm({ sample, onSaved }: Props) {
  const { t } = useTranslation("testPreparation");
  const { userId } = useAuth();
  const [form, setForm] = useState<TestPreparationFormState>({ technique: "PourPlate" });
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [signing, setSigning] = useState(false);

  const assignedIds = sample.assignedAnalystIds ?? [];
  const isAssignedToOther =
    assignedIds.length > 0 && !assignedIds.includes(userId as number);

  const setField = (k: keyof TestPreparationFormState, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const [diluentSel, setDiluentSel] = useState<PreparationMaterialSelection>(EMPTY_SELECTION);
  const [neutralizerSel, setNeutralizerSel] = useState<PreparationMaterialSelection>(EMPTY_SELECTION);
  const diluentLots = useMasterLots(diluentSel);
  const neutralizerLots = useMasterLots(neutralizerSel);

  // A lot is needed once the lot list for the chosen master is known to require one.
  const lotsReady =
    (!diluentLots.group?.lotRequired || diluentLots.lotId != null) &&
    (!neutralizerLots.group?.lotRequired || neutralizerLots.lotId != null);
  const canSubmit =
    isSelectionComplete(diluentSel) && isSelectionComplete(neutralizerSel) && lotsReady &&
    (diluentSel.source === "ContainedInMedium" || diluentLots.group != null) &&
    (neutralizerSel.source === "ContainedInMedium" || neutralizerLots.group != null);

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const save = async (password: string) => {
    setMessage(null);
    await SamplePreparationService.prepare({
      sampleId: sample.sampleId, amount: Number(form.amount), technique: form.technique,
      filtrationVolume: form.filtrationVolume ? Number(form.filtrationVolume) : undefined,
      washingVolume: form.washingVolume ? Number(form.washingVolume) : undefined,
      ...selectionPayload("diluent", diluentSel),
      ...selectionPayload("neutralizer", neutralizerSel),
      diluentLotId: diluentLots.group?.lotRequired ? diluentLots.lotId : null,
      neutralizerLotId: neutralizerLots.group?.lotRequired ? neutralizerLots.lotId : null,
      password
    });
    setSigning(false);
    setMessage({ text: t("manual.saved"), ok: true });
    onSaved();
  };

  return (
    <Box>
      {isAssignedToOther && (
        <Alert severity="warning" sx={{ mb: 2, fontSize: 13 }}>
          <Trans
            i18nKey="assignmentRule"
            ns="testPreparation"
            values={{
              names: sample.assignedAnalystNames?.length
                ? sample.assignedAnalystNames.join(t("common:listSeparator"))
                : t("userFallback", { id: assignedIds[0] })
            }}
            components={{ strong: <strong /> }}
          />
        </Alert>
      )}

      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 2 }}>
        <TextField label={t("fields.sampleAmount")} value={form.amount ?? ""} onChange={(e) => setField("amount", e.target.value)} disabled={isAssignedToOther} />
        <Select value={form.technique} onChange={(e) => setField("technique", e.target.value)} disabled={isAssignedToOther} inputProps={{ "aria-label": t("fields.technique") }}>
          <MenuItem value="PourPlate">{t("techniques.PourPlate")}</MenuItem>
          <MenuItem value="Filtration">{t("techniques.Filtration")}</MenuItem>
        </Select>
      </Box>

      {form.technique === "Filtration" && (
        <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2, mt: 2 }}>
          <TextField label={t("fields.filtrationVolumeMl")} value={form.filtrationVolume ?? ""} onChange={(e) => setField("filtrationVolume", e.target.value)} disabled={isAssignedToOther} />
          <TextField label={t("fields.washingVolumeMl")} value={form.washingVolume ?? ""} onChange={(e) => setField("washingVolume", e.target.value)} disabled={isAssignedToOther} />
        </Box>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mt: 2 }}>
        <Box sx={{ display: "grid", gap: 1.5, alignContent: "start" }}>
          <PreparationMaterialPicker label={t("fields.diluent")} value={diluentSel} onChange={setDiluentSel} disabled={isAssignedToOther} />
          {diluentLots.error && <Alert severity="error" sx={{ fontSize: 12 }}>{diluentLots.error}</Alert>}
          {diluentLots.group && (
            <PreparationLotPicker label={t("fields.diluent")} group={diluentLots.group} value={diluentLots.lotId}
              onChange={diluentLots.setLotId} disabled={isAssignedToOther} />
          )}
        </Box>
        <Box sx={{ display: "grid", gap: 1.5, alignContent: "start" }}>
          <PreparationMaterialPicker label={t("fields.neutralizer")} value={neutralizerSel} onChange={setNeutralizerSel}
            allowContainedInMedium disabled={isAssignedToOther} />
          {neutralizerLots.error && <Alert severity="error" sx={{ fontSize: 12 }}>{neutralizerLots.error}</Alert>}
          {neutralizerLots.group && (
            <PreparationLotPicker label={t("fields.neutralizer")} group={neutralizerLots.group} value={neutralizerLots.lotId}
              onChange={neutralizerLots.setLotId} disabled={isAssignedToOther} />
          )}
        </Box>
      </Box>

      <Alert severity="info" sx={{ mt: 2, fontSize: 12 }}>
        <Trans
          i18nKey="manual.noConfigInfo"
          ns="testPreparation"
          values={{ itemName: sample.itemName ?? t("manual.thisItem") }}
          components={{ strong: <strong /> }}
        />
      </Alert>

      <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
        <Button variant="contained" onClick={() => setSigning(true)} disabled={isAssignedToOther || !canSubmit}>
          {t("actions.startTesting")}
        </Button>
      </Box>

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "testPreparation")("manual.signatureMeaning")}
        meaningStatementTranslation={t("manual.signatureMeaning")}
        onCancel={() => setSigning(false)}
        onConfirm={save}
      />
    </Box>
  );
}
