import React, { useState, useEffect } from "react";
import {
  Button,
  Stack,
  TextField,
  Typography,
  Alert
} from "@mui/material";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { serverMessage } from "../../../utils/errorMessage";
import { MaterialMasterService, type MaterialMasterEntry } from "../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import { WorkingStandardService } from "../services/WorkingStandardService";
import type {
  WorkingStandardLotDto,
  WorkingStandardQualificationDto,
  EligibleSourceSampleDto,
  CreateQualificationRequest
} from "../types";
import {
  type NewQualificationFormState,
  isNewQualificationSaveEnabled
} from "./newDialog/qualificationFormState";
import { InitialQualificationFields } from "./newDialog/InitialQualificationFields";


interface NewQualificationDialogProps {
  open: boolean;
  targetLot?: WorkingStandardLotDto | null;
  onClose: () => void;
  onSuccess: (qualification: WorkingStandardQualificationDto) => void;
}

export const NewQualificationDialog: React.FC<NewQualificationDialogProps> = ({
  open,
  targetLot,
  onClose,
  onSuccess
}) => {
  const { t } = useTranslation("workingStandards");
  const isRequalification = Boolean(targetLot);
  const [sourceMode, setSourceMode] = useState<"Manual" | "Received">("Manual");
  const [materialMasterEntryId, setMaterialMasterEntryId] = useState<number | "">("");
  const [sourceSampleId, setSourceSampleId] = useState<number | null>(null);
  const [sourceMaterialName, setSourceMaterialName] = useState("");
  const [sourceBatchNumber, setSourceBatchNumber] = useState("");
  const [quantityGrams, setQuantityGrams] = useState<string>("");
  const [location, setLocation] = useState("");
  const [moisturePercent, setMoisturePercent] = useState<string>("");

  const [masterEntries, setMasterEntries] = useState<MaterialMasterEntry[]>([]);
  const [sourceSamples, setSourceSamples] = useState<EligibleSourceSampleDto[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setError(null);
      setSubmitting(false);
      if (targetLot) {
        setSourceMaterialName(targetLot.materialName);
        setSourceBatchNumber(targetLot.batchNumber);
        setMoisturePercent("");
      } else {
        setSourceMode("Manual");
        setMaterialMasterEntryId("");
        setSourceSampleId(null);
        setSourceMaterialName("");
        setSourceBatchNumber("");
        setQuantityGrams("");
        setLocation("");
        setMoisturePercent("");
      }

      MaterialMasterService.getAll("ReferenceStandard", true)
        .then(setMasterEntries)
        .catch(() => setMasterEntries([]));
    }
  }, [open, targetLot]);

  useEffect(() => {
    if (open && !targetLot && sourceMode === "Received") {
      WorkingStandardService.getEligibleSourceSamples()
        .then(setSourceSamples)
        .catch(() => setSourceSamples([]));
    }
  }, [open, targetLot, sourceMode]);

  const handleSourceSampleChange = (sample: EligibleSourceSampleDto | null) => {
    if (sample) {
      setSourceSampleId(sample.sampleId);
      setSourceMaterialName(sample.materialName);
      setSourceBatchNumber(sample.batchNumber ?? "");
    } else {
      setSourceSampleId(null);
      setSourceMaterialName("");
      setSourceBatchNumber("");
    }
  };

  const handleSourceModeChange = (mode: "Manual" | "Received") => {
    setSourceMode(mode);
    setSourceSampleId(null);
    setSourceMaterialName("");
    setSourceBatchNumber("");
  };

  const formState: NewQualificationFormState = {
    kind: isRequalification ? "Requalification" : "Initial",
    sourceMode,
    materialMasterEntryId,
    sourceSampleId,
    sourceMaterialName,
    sourceBatchNumber,
    quantityGrams,
    location,
    moisturePercent,
    workingStandardMaterialId: targetLot?.materialId
  };

  const isValid = isNewQualificationSaveEnabled(formState);

  const handleSave = async () => {
    if (!isValid || submitting) return;
    setSubmitting(true);
    setError(null);

    try {
      const payload: CreateQualificationRequest = isRequalification
        ? {
            kind: "Requalification",
            workingStandardMaterialId: targetLot?.materialId,
            moisturePercent: moisturePercent ? Number(moisturePercent) : undefined
          }
        : {
            kind: "Initial",
            materialMasterEntryId: typeof materialMasterEntryId === "number" ? materialMasterEntryId : undefined,
            sourceSampleId: sourceMode === "Received" ? (sourceSampleId ?? undefined) : undefined,
            sourceMaterialName: sourceMaterialName.trim(),
            sourceBatchNumber: sourceBatchNumber.trim(),
            quantityGrams: quantityGrams ? Number(quantityGrams) : undefined,
            location: location.trim(),
            moisturePercent: moisturePercent ? Number(moisturePercent) : undefined
          };

      const result = await WorkingStandardService.createQualification(payload);
      toast.success(isRequalification ? t("newDialog.toastRequalified") : t("newDialog.toastCreated"));
      onSuccess(result);
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? t("newDialog.createFailed");
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      title={isRequalification ? t("newDialog.titleRequalify") : t("newDialog.titleNew")}
      onClose={onClose}
      maxWidth="sm"
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>{t("actions.cancel")}</Button>
          <Button variant="contained" onClick={handleSave} disabled={!isValid || submitting}>
            {submitting ? t("actions.saving") : t("actions.save")}
          </Button>
        </>
      }
    >
      <Stack spacing={2.5} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}

        {isRequalification && targetLot ? (
          <Alert severity="info">
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
              <Trans
                i18nKey="newDialog.requalifyingLot"
                ns="workingStandards"
                values={{ code: targetLot.code }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Typography variant="body2">
              <Trans
                i18nKey="newDialog.materialLine"
                ns="workingStandards"
                values={{ name: targetLot.materialName, code: targetLot.masterEntryCode }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Typography variant="body2">
              <Trans
                i18nKey="newDialog.batchLine"
                ns="workingStandards"
                values={{ batch: targetLot.batchNumber }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Alert>
        ) : (
          <InitialQualificationFields
            sourceMode={sourceMode}
            onSourceModeChange={handleSourceModeChange}
            sourceSampleId={sourceSampleId}
            sourceSamples={sourceSamples}
            onSourceSampleChange={handleSourceSampleChange}
            sourceMaterialName={sourceMaterialName}
            onSourceMaterialNameChange={setSourceMaterialName}
            sourceBatchNumber={sourceBatchNumber}
            onSourceBatchNumberChange={setSourceBatchNumber}
            materialMasterEntryId={materialMasterEntryId}
            onMaterialMasterEntryIdChange={setMaterialMasterEntryId}
            masterEntries={masterEntries}
            quantityGrams={quantityGrams}
            onQuantityGramsChange={setQuantityGrams}
            location={location}
            onLocationChange={setLocation}
          />
        )}

        <TextField
          fullWidth
          size="small"
          type="number"
          label={t("newDialog.moistureLabel")}
          value={moisturePercent}
          onChange={(e) => setMoisturePercent(e.target.value)}
          required={isRequalification}
          helperText={isRequalification ? t("newDialog.moistureHelperRequalify") : t("newDialog.moistureHelperInitial")}
        />
      </Stack>
    </FloatingDialog>
  );
};
