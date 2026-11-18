import { useEffect, useState } from "react";
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Typography,
  Alert,
  Stack
} from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { UploadEquipmentDocumentDialog } from "./UploadEquipmentDocumentDialog";
import { formatLabDate } from "../../../../utils/formatDate";
import type {
  EquipmentDocument,
  EquipmentTaskResponse,
  RecordCalibrationRequest
} from "../types/equipmentTypes";
import { DateField } from "../../../../components/DateField";
import { useTranslation } from "../../../../i18n";

interface Props {
  open: boolean;
  equipmentId: number;
  equipmentCode?: string;
  onClose: () => void;
  onSuccess: (task: EquipmentTaskResponse) => void;
}

export function RecordCalibrationDialog({
  open,
  equipmentId,
  equipmentCode = "",
  onClose,
  onSuccess
}: Props) {
  const { t, i18n } = useTranslation("inventory");
  const [documents, setDocuments] = useState<EquipmentDocument[]>([]);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [selectedDocId, setSelectedDocId] = useState<string>("");
  const [calibratedOn, setCalibratedOn] = useState<string>("");
  const [certificateNextDue, setCertificateNextDue] = useState<string>("");
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);

  const loadDocuments = async (autoSelectNewest = false) => {
    try {
      setLoadingDocs(true);
      const docs = await EquipmentInventoryService.getDocuments(equipmentId);
      const currentCerts = docs.filter(
        (d) => d.status === "Current" && d.documentType === "CalibrationCertificate"
      );
      setDocuments(currentCerts);
      if (autoSelectNewest && currentCerts.length > 0) {
        // Find latest uploaded
        const sorted = [...currentCerts].sort(
          (a, b) => new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime()
        );
        setSelectedDocId(sorted[0].id.toString());
      }
    } catch {
      setError(t("equipment.recordCalibrationDialog.errors.loadError"));
    } finally {
      setLoadingDocs(false);
    }
  };

  useEffect(() => {
    if (open) {
      loadDocuments();
      setSelectedDocId("");
      setCalibratedOn("");
      setCertificateNextDue("");
      setSigning(false);
      setError(null);
    }
  }, [open, equipmentId]);

  const canContinue = Boolean(selectedDocId && calibratedOn);

  const handleContinueToSign = () => {
    setError(null);

    if (!selectedDocId) {
      setError(t("equipment.recordCalibrationDialog.errors.certificateRequired"));
      return;
    }
    if (!calibratedOn) {
      setError(t("equipment.recordCalibrationDialog.errors.dateRequired"));
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    if (calibratedOn > today) {
      setError(t("equipment.recordCalibrationDialog.errors.dateFuture"));
      return;
    }

    setSigning(true);
  };

  const selectedDoc = documents.find((d) => d.id.toString() === selectedDocId);
  const fileName = selectedDoc?.originalFileName ?? "";
  const enT = i18n.getFixedT("en", "inventory");
  const meaningKey = certificateNextDue
    ? "equipment.recordCalibrationDialog.meaningStatementWithNextDue"
    : "equipment.recordCalibrationDialog.meaningStatement";
  const meaningParams = {
    code: equipmentCode,
    date: calibratedOn,
    fileName,
    nextDue: certificateNextDue
  };
  const meaningStatement = enT(meaningKey, meaningParams);
  const meaningStatementTranslation = t(meaningKey, meaningParams);

  const handleSignConfirm = async (password: string) => {
    const payload: RecordCalibrationRequest = {
      certificateDocumentId: Number(selectedDocId),
      calibratedOn,
      certificateNextDue: certificateNextDue || null,
      password
    };
    const task = await EquipmentPlanService.recordCalibration(equipmentId, payload);
    setSigning(false);
    onSuccess(task);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signing}
        title={t("equipment.recordCalibrationDialog.title")}
        onClose={onClose}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={onClose} color="inherit">
              {t("common.cancel")}
            </Button>
            <Button
              id="record-calibration-submit"
              variant="contained"
              onClick={handleContinueToSign}
              disabled={!canContinue || loadingDocs}
            >
              {t("equipment.recordCalibrationDialog.continueToSign")}
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          {t("equipment.recordCalibrationDialog.subtitle")}
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Stack spacing={2.5}>
          {/* Certificate Selection + Upload Button */}
          <Box>
            <Box sx={{ display: "flex", gap: 1, alignItems: "flex-start" }}>
              <FormControl size="small" fullWidth>
                <InputLabel id="cert-select-label">{t("equipment.recordCalibrationDialog.certificateLabel")}</InputLabel>
                <Select
                  labelId="cert-select-label"
                  id="record-cal-cert-select"
                  value={selectedDocId}
                  label={t("equipment.recordCalibrationDialog.certificateLabel")}
                  onChange={(e) => setSelectedDocId(e.target.value)}
                  disabled={loadingDocs}
                >
                  {documents.length === 0 ? (
                    <MenuItem value="" disabled>
                      {loadingDocs ? t("equipment.recordCalibrationDialog.loadingCertificates") : t("equipment.recordCalibrationDialog.noCertificates")}
                    </MenuItem>
                  ) : (
                    documents.map((d) => (
                      <MenuItem key={d.id} value={d.id.toString()}>
                        {d.originalFileName} ({formatLabDate(d.uploadedAt)})
                      </MenuItem>
                    ))
                  )}
                </Select>
              </FormControl>
              <Button
                variant="outlined"
                size="small"
                startIcon={<UploadFileIcon fontSize="small" />}
                onClick={() => setUploadOpen(true)}
                sx={{ minWidth: 140, height: 40, textTransform: "none", fontSize: 12 }}
              >
                {t("equipment.recordCalibrationDialog.uploadNew")}
              </Button>
            </Box>
            {documents.length === 0 && !loadingDocs && (
              <Typography sx={{ fontSize: 12, color: "warning.main", mt: 0.5 }}>
                {t("equipment.recordCalibrationDialog.certificateRequiredHint")}
              </Typography>
            )}
          </Box>

          {/* Calibration Date */}
          <DateField
            label={t("equipment.recordCalibrationDialog.calibrationDate")}
            required
            fullWidth
            value={calibratedOn}
            onChange={(v) => setCalibratedOn(v)}
            helperText={t("equipment.recordCalibrationDialog.calibrationDateHelper")}
          />

          {/* Certificate Next-Due Date (Optional) */}
          <DateField
            label={t("equipment.recordCalibrationDialog.nextDueLabel")}
            fullWidth
            value={certificateNextDue}
            onChange={(v) => setCertificateNextDue(v)}
            helperText={t("equipment.recordCalibrationDialog.nextDueHelper")}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        onCancel={() => setSigning(false)}
        onConfirm={handleSignConfirm}
      />

      <UploadEquipmentDocumentDialog
        open={uploadOpen}
        equipmentId={equipmentId}
        onClose={() => setUploadOpen(false)}
        onSuccess={() => {
          setUploadOpen(false);
          loadDocuments(true);
        }}
      />
    </>
  );
}
