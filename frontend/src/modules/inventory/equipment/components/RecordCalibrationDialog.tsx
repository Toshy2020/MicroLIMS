import { useEffect, useState } from "react";
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
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
      setError("Failed to load calibration certificates.");
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
      setError("Please select a calibration certificate.");
      return;
    }
    if (!calibratedOn) {
      setError("Calibration date is required.");
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    if (calibratedOn > today) {
      setError("Calibration date cannot be in the future.");
      return;
    }

    setSigning(true);
  };

  const selectedDoc = documents.find((d) => d.id.toString() === selectedDocId);
  const fileName = selectedDoc?.originalFileName ?? "";
  const meaningStatement = `I confirm the calibration of ${equipmentCode} on ${calibratedOn} is recorded with certificate ${fileName}${certificateNextDue ? `, next due ${certificateNextDue}` : ""}.`;

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
        title="Record Calibration"
        onClose={onClose}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={onClose} color="inherit">
              Cancel
            </Button>
            <Button
              id="record-calibration-submit"
              variant="contained"
              onClick={handleContinueToSign}
              disabled={!canContinue || loadingDocs}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          Attach a current calibration certificate, enter the calibration date, and electronically sign with your password.
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
                <InputLabel id="cert-select-label">Calibration Certificate</InputLabel>
                <Select
                  labelId="cert-select-label"
                  id="record-cal-cert-select"
                  value={selectedDocId}
                  label="Calibration Certificate"
                  onChange={(e) => setSelectedDocId(e.target.value)}
                  disabled={loadingDocs}
                >
                  {documents.length === 0 ? (
                    <MenuItem value="" disabled>
                      {loadingDocs ? "Loading certificates..." : "No current certificates found"}
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
                Upload New
              </Button>
            </Box>
            {documents.length === 0 && !loadingDocs && (
              <Typography sx={{ fontSize: 12, color: "warning.main", mt: 0.5 }}>
                A current calibration certificate is required. Click &quot;Upload New&quot; to upload one.
              </Typography>
            )}
          </Box>

          {/* Calibration Date */}
          <TextField
            id="record-cal-date"
            label="Calibration Date"
            type="date"
            size="small"
            required
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            value={calibratedOn}
            onChange={(e) => setCalibratedOn(e.target.value)}
            helperText="Date when calibration was performed (cannot be in the future)"
          />

          {/* Certificate Next-Due Date (Optional) */}
          <TextField
            id="record-cal-next-due"
            label="Certificate Next-Due Date (Optional)"
            type="date"
            size="small"
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            value={certificateNextDue}
            onChange={(e) => setCertificateNextDue(e.target.value)}
            helperText="When specified on the vendor certificate, overrides the calculated interval due date"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement={meaningStatement}
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
