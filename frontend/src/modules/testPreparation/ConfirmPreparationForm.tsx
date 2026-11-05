import { useEffect, useState } from "react";
import { Box, Button, Alert, Typography, Chip, CircularProgress } from "@mui/material";
import { SamplePreparationService, type PreparationLotOptions } from "./services/SamplePreparationService";
import { PreparationLotPicker } from "./components/PreparationLotPicker";
import { PreparationStepsSummary } from "./PreparationStepsSummary";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useAuth } from "../../contexts/AuthContext";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";

interface Props {
  sample: {
    sampleId: number;
    // Every analyst on the sample's tests - any of them may prepare it.
    assignedAnalystIds?: number[];
    assignedAnalystNames?: string[];
  };
  config: ItemPreparationConfiguration;
  onSaved: () => void;
}

// Confirm-only. The configured steps are shown as they stand and signed
// for as-is - correcting them is a Section Head configuration change under
// Laboratory Configuration, not an analyst-side override here.
export function ConfirmPreparationForm({ sample, config, onSaved }: Props) {
  const { userId } = useAuth();
  const [signing, setSigning] = useState(false);
  const [lots, setLots] = useState<PreparationLotOptions | null>(null);
  const [lotsError, setLotsError] = useState<string | null>(null);
  const [diluentLotId, setDiluentLotId] = useState<number | null>(null);
  const [neutralizerLotId, setNeutralizerLotId] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLots(null);
    setLotsError(null);
    setDiluentLotId(null);
    setNeutralizerLotId(null);
    SamplePreparationService.getLotOptions({ sampleId: sample.sampleId })
      .then((o) => { if (!cancelled) setLots(o); })
      .catch((e: any) => {
        if (!cancelled) setLotsError(e?.response?.data?.message ?? "Could not load the available lots.");
      });
    return () => { cancelled = true; };
  }, [sample.sampleId]);

  const needsDiluentLot = Boolean(lots?.diluent.lotRequired);
  const needsNeutralizerLot = Boolean(lots?.neutralizer.lotRequired);
  const lotsComplete =
    lots != null &&
    (!needsDiluentLot || diluentLotId != null) &&
    (!needsNeutralizerLot || neutralizerLotId != null);

  const assignedIds = sample.assignedAnalystIds ?? [];
  const isAssignedToOther =
    assignedIds.length > 0 && !assignedIds.includes(userId as number);

  // Errors propagate to SignatureDialog, which surfaces the server message
  // and keeps itself open with the password cleared.
  const confirm = async (password: string) => {
    await SamplePreparationService.confirm({
      sampleId: sample.sampleId,
      password,
      diluentLotId: needsDiluentLot ? diluentLotId : null,
      neutralizerLotId: needsNeutralizerLot ? neutralizerLotId : null
    });
    setSigning(false);
    onSaved();
  };

  return (
    <Box>
      {isAssignedToOther && (
        <Alert severity="warning" sx={{ mb: 2, fontSize: 13 }}>
          <strong>Sample Assignment Rule:</strong> This sample is currently assigned to{" "}
          <strong>{sample.assignedAnalystNames?.length ? sample.assignedAnalystNames.join(", ") : `User #${assignedIds[0]}`}</strong>.
          Only an assigned analyst may prepare this sample, unless reassigned by an authorized Section Head.
        </Alert>
      )}

      {config.approvalStatus === "PendingReview" && (
        <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
          This item's preparation configuration is still awaiting Section Head approval. It is in effect and
          can be confirmed now - approval is a separate review and does not hold up testing.
        </Alert>
      )}

      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Configured Preparation Steps
        </Typography>
        <Chip
          size="small"
          label={config.approvalStatus === "Approved" ? "Approved" : "Pending Approval"}
          color={config.approvalStatus === "Approved" ? "success" : "warning"}
          sx={{ height: 20, fontSize: 12 }}
        />
      </Box>

      <PreparationStepsSummary config={config} />

      {lotsError && <Alert severity="error" sx={{ mt: 2, fontSize: 13 }}>{lotsError}</Alert>}
      {!lots && !lotsError && (
        <Box sx={{ display: "flex", justifyContent: "center", mt: 2 }}><CircularProgress size={20} /></Box>
      )}
      {lots?.needsMaterialMapping && (
        <Alert severity="warning" sx={{ mt: 2, fontSize: 13 }}>
          This item's diluent/neutralizer is not linked to master data, so no lot can be recorded for this
          sample. A Section Head can link them under Laboratory Configuration → Items.
        </Alert>
      )}
      {lots && !lots.needsMaterialMapping && (needsDiluentLot || needsNeutralizerLot) && (
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, mt: 2 }}>
          <Box sx={{ display: "grid", gap: 1.5, alignContent: "start" }}>
            <PreparationLotPicker label="Diluent" group={lots.diluent} value={diluentLotId} onChange={setDiluentLotId} disabled={isAssignedToOther} />
          </Box>
          <Box sx={{ display: "grid", gap: 1.5, alignContent: "start" }}>
            <PreparationLotPicker label="Neutralizer" group={lots.neutralizer} value={neutralizerLotId} onChange={setNeutralizerLotId} disabled={isAssignedToOther} />
          </Box>
        </Box>
      )}

      <Typography variant="caption" sx={{ display: "block", mt: 2, color: "text.secondary" }}>
        Confirming records these exact values against this sample. Later changes to the item's configuration
        will not alter this record.
      </Typography>

      <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
        <Button variant="contained" onClick={() => setSigning(true)} disabled={isAssignedToOther || !lotsComplete}>
          Confirm & Start Testing
        </Button>
      </Box>

      <SignatureDialog
        open={signing}
        meaningStatement="I confirm the preparation steps shown above are the steps performed for this sample."
        onCancel={() => setSigning(false)}
        onConfirm={confirm}
      />
    </Box>
  );
}
