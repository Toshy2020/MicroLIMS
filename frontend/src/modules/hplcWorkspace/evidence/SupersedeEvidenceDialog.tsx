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
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcEvidenceDto } from "../types";

export interface SupersedeEvidenceDialogProps {
  open: boolean;
  targetEvidence: HplcEvidenceDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function SupersedeEvidenceDialog({
  open,
  targetEvidence,
  onClose,
  onSuccess
}: SupersedeEvidenceDialogProps) {
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
      await HplcWorkspaceService.supersedeEvidence(
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
              accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg"
              ref={replaceInputRef}
              onChange={handleFileChange}
              style={{ display: "none" }}
              id="supersede-file-input"
            />
            <label htmlFor="supersede-file-input">
              <Button
                variant="outlined"
                component="span"
                size="small"
                startIcon={<CloudUploadIcon />}
                sx={{ textTransform: "none" }}
              >
                Select Replacement File
              </Button>
            </label>
            {replacementFile && (
              <Typography variant="body2" sx={{ mt: 1, fontWeight: 600 }}>
                Selected: {replacementFile.name} ({(replacementFile.size / (1024 * 1024)).toFixed(2)} MB)
              </Typography>
            )}
          </Box>

          <TextField
            label="Supersede Reason *"
            placeholder="Explain why this report is being replaced..."
            multiline
            rows={3}
            fullWidth
            required
            size="small"
            value={supersedeReason}
            onChange={(e) => setSupersedeReason(e.target.value)}
            helperText="A reason is required under GxP data integrity rules."
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button
          onClick={handleClose}
          disabled={superseding}
          sx={{ textTransform: "none" }}
        >
          Cancel
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleSupersede}
          disabled={!replacementFile || !supersedeReason.trim() || superseding}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {superseding ? "Superseding..." : "Confirm Replacement"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
