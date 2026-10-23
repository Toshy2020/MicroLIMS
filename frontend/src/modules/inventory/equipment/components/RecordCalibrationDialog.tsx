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
  CircularProgress,
  Stack
} from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import VerifiedIcon from "@mui/icons-material/Verified";
import { FloatingDialog } from "../../../../components/FloatingDialog";
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
  onClose: () => void;
  onSuccess: (task: EquipmentTaskResponse) => void;
}

export function RecordCalibrationDialog({
  open,
  equipmentId,
  onClose,
  onSuccess
}: Props) {
  const [documents, setDocuments] = useState<EquipmentDocument[]>([]);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [selectedDocId, setSelectedDocId] = useState<string>("");
  const [calibratedOn, setCalibratedOn] = useState<string>("");
  const [certificateNextDue, setCertificateNextDue] = useState<string>("");
  const [password, setPassword] = useState<string>("");
  const [submitting, setSubmitting] = useState(false);
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
      setPassword("");
      setError(null);
    }
  }, [open, equipmentId]);

  const handleSubmit = async () => {
    setError(null);

    if (!selectedDocId) {
      setError("Please select a calibration certificate.");
      return;
    }
    if (!calibratedOn) {
      setError("Calibration date is required.");
      return;
    }
    if (!password) {
      setError("Password is required.");
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    if (calibratedOn > today) {
      setError("Calibration date cannot be in the future.");
      return;
    }

    const payload: RecordCalibrationRequest = {
      certificateDocumentId: Number(selectedDocId),
      calibratedOn,
      certificateNextDue: certificateNextDue || null,
      password
    };

    setSubmitting(true);
    try {
      const task = await EquipmentPlanService.recordCalibration(equipmentId, payload);
      onSuccess(task);
      onClose();
    } catch (err: any) {
      setError(
        err?.response?.data?.message ??
          err?.message ??
          "Failed to record calibration."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <FloatingDialog
        open={open}
        title="Record Calibration"
        onClose={submitting ? () => {} : onClose}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={onClose} disabled={submitting} color="inherit">
              Cancel
            </Button>
            <Button
              id="record-calibration-submit"
              variant="contained"
              onClick={handleSubmit}
              disabled={submitting}
              startIcon={
                submitting ? (
                  <CircularProgress size={16} color="inherit" />
                ) : (
                  <VerifiedIcon />
                )
              }
            >
              {submitting ? "Signing..." : "Record Calibration"}
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

          {/* Password (Electronic Signature) */}
          <TextField
            id="record-cal-password"
            label="Password"
            type="password"
            size="small"
            required
            fullWidth
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            helperText="Enter your login password to electronically sign this calibration record"
          />
        </Stack>
      </FloatingDialog>

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
