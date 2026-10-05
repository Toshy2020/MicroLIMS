import { useState, useRef } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  Stack
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpEvidenceDto } from "../types";

export interface IcpSupersedeEvidenceDialogProps {
  open: boolean;
  targetEvidence: IcpEvidenceDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function IcpSupersedeEvidenceDialog({
  open,
  targetEvidence,
  onClose,
  onSuccess
}: IcpSupersedeEvidenceDialogProps) {
  const replaceInputRef = useRef<HTMLInputElement>(null);
  const [replacementFile, setReplacementFile] = useState<File | null>(null);
  const [supersedeReason, setSupersedeReason] = useState("");
  const [superseding, setSuperseding] = useState(false);
  const [supersedeError, setSupersedeError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSupersedeError(null);
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      if (file.size > MAX_FILE_SIZE_BYTES) {
        setSupersedeError("File exceeds the 25 MB limit.");
        setReplacementFile(null);
        return;
      }
      setReplacementFile(file);
    }
  };

  const handleClose = () => {
    if (superseding) return;
    setReplacementFile(null);
    setSupersedeReason("");
    setSupersedeError(null);
    onClose();
  };

  const handleSupersede = async () => {
    if (!targetEvidence || !replacementFile || !supersedeReason.trim()) return;

    setSuperseding(true);
    setSupersedeError(null);
    try {
      await IcpWorkspaceService.supersedeEvidence(
        targetEvidence.id,
        replacementFile,
        supersedeReason.trim()
      );
      handleClose();
      onSuccess();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setSupersedeError(e.response?.data?.message ?? e.message ?? "Replacement upload failed.");
    } finally {
      setSuperseding(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        Supersede Evidence File
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {supersedeError && <Alert severity="error">{supersedeError}</Alert>}

          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Replacing: <strong>{targetEvidence?.fileName}</strong>. Old versions remain visible in the audit trail.
          </Typography>

          <Box>
            <input
              type="file"
              ref={replaceInputRef}
              style={{ display: "none" }}
              onChange={handleFileChange}
              accept=".pdf,.png,.jpg,.jpeg,.txt,.csv"
            />
            <Button
              variant="outlined"
              startIcon={<CloudUploadIcon />}
              onClick={() => replaceInputRef.current?.click()}
              sx={{ textTransform: "none" }}
            >
              {replacementFile ? replacementFile.name : "Select Replacement File"}
            </Button>
          </Box>

          <TextField
            label="Reason for Superseding"
            placeholder="Explain why this file is being replaced (e.g. recalculated baseline, re-exported chromatogram)..."
            multiline
            rows={3}
            fullWidth
            required
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 500 } }}
            helperText={`${supersedeReason.length}/500 characters`}
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={superseding}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="warning"
          onClick={handleSupersede}
          disabled={!replacementFile || !supersedeReason.trim() || superseding}
        >
          {superseding ? "Uploading..." : "Supersede File"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
