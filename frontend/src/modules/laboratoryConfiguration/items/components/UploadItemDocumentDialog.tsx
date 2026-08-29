import { useState, useEffect } from "react";
import {
  Button,
  TextField,
  Select,
  MenuItem,
  Stack,
  Box,
  Typography,
  Alert,
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import { ItemDocumentType, ItemDocumentService } from "../services/ItemDocumentService";
import { FloatingDialog } from "../../../../components/FloatingDialog";

interface UploadItemDocumentDialogProps {
  open: boolean;
  itemId: number;
  itemName: string;
  defaultDocType?: ItemDocumentType;
  onClose: () => void;
  onSuccess: () => void;
}

export function UploadItemDocumentDialog({
  open,
  itemId,
  itemName,
  defaultDocType = ItemDocumentType.Sop,
  onClose,
  onSuccess,
}: UploadItemDocumentDialogProps) {
  const [docType, setDocType] = useState<ItemDocumentType>(defaultDocType);
  const [version, setVersion] = useState("Rev 01");
  const [effectiveDate, setEffectiveDate] = useState<string>("");
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setDocType(defaultDocType);
      setVersion("Rev 01");
      setEffectiveDate(new Date().toISOString().split("T")[0]);
      setFile(null);
      setError(null);
    }
  }, [open, defaultDocType]);

  const handleUpload = async () => {
    if (!file) {
      setError("Please select a document file to upload.");
      return;
    }

    setUploading(true);
    setError(null);

    try {
      await ItemDocumentService.uploadDocument(itemId, docType, version, effectiveDate || null, file);
      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to upload document.");
    } finally {
      setUploading(false);
    }
  };

  const docTypeName = docType === ItemDocumentType.Sop ? "SOP" : "Verification Report";

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      titleSx={{ fontWeight: 700, fontSize: 15 }}
      title={`Upload Controlled ${docTypeName}`}
      actions={
        <>
          <Button onClick={onClose} disabled={uploading} color="inherit">
            Cancel
          </Button>
          <Button variant="contained" onClick={handleUpload} disabled={uploading || !file}>
            {uploading ? "Uploading..." : "Upload"}
          </Button>
        </>
      }
    >
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Item: <strong>{itemName}</strong>
          </Typography>

          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
              Document Type
            </Typography>
            <Select
              size="small"
              value={docType}
              onChange={(e) => setDocType(e.target.value as ItemDocumentType)}
              fullWidth
            >
              <MenuItem value={ItemDocumentType.Sop}>SOP</MenuItem>
              <MenuItem value={ItemDocumentType.VerificationReport}>Verification Report</MenuItem>
            </Select>
          </Box>

          <Stack direction="row" spacing={1.5}>
            <TextField
              size="small"
              label="Version"
              placeholder="e.g. Rev 01"
              value={version}
              onChange={(e) => setVersion(e.target.value)}
              sx={{ flex: 1 }}
              required
            />
            <TextField
              size="small"
              label="Effective Date"
              type="date"
              value={effectiveDate}
              onChange={(e) => setEffectiveDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
              sx={{ flex: 1 }}
            />
          </Stack>

          <Box
            sx={{
              p: 2,
              border: "1px dashed",
              borderColor: file ? "primary.main" : "divider",
              borderRadius: 1.5,
              textAlign: "center",
              bgcolor: file ? "action.hover" : "transparent",
            }}
          >
            <input
              type="file"
              id="item-doc-upload-input"
              style={{ display: "none" }}
              accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
              onChange={(e) => {
                if (e.target.files && e.target.files[0]) {
                  setFile(e.target.files[0]);
                }
              }}
            />
            <label htmlFor="item-doc-upload-input" style={{ cursor: "pointer", display: "block" }}>
              <CloudUploadIcon sx={{ fontSize: 32, color: file ? "primary.main" : "text.secondary", mb: 0.5 }} />
              <Typography variant="body2" sx={{ fontWeight: 600, color: "text.primary" }}>
                {file ? file.name : "Choose File"}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {file ? `${(file.size / 1024).toFixed(1)} KB` : "PDF, Word, Excel, or Image (Max 25MB)"}
              </Typography>
            </label>
          </Box>
        </Stack>
    </FloatingDialog>
  );
}
