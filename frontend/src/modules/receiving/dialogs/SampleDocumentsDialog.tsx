import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Box, Button, Chip, CircularProgress, Divider, IconButton, Stack, TextField, Tooltip, Typography } from "@mui/material";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import BlockIcon from "@mui/icons-material/Block";
import UploadFileOutlinedIcon from "@mui/icons-material/UploadFileOutlined";
import { toast } from "sonner";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { ReceiveService } from "../services/ReceiveService";
import type { SampleDocument, SampleRecord } from "../types/receivingTypes";

interface Props {
  sample: SampleRecord;
  onClose: () => void;
}

const errorText = (err: any, fallback: string) => err?.response?.data?.message ?? err?.message ?? fallback;

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// The documents attached to one sample (the supplier's CoA). Whether the caller
// may open them, and which laboratories they cover, is decided by the server.
export function SampleDocumentsDialog({ sample, onClose }: Props) {
  const { permissions } = useAuth();
  const canChange = permissions.includes(PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB);
  // Same permission as voiding the sample itself (Section Head, Supervisor, Administrator).
  const canVoid = permissions.includes(PERMISSIONS.SAMPLES_CORRECT);
  const fileInput = useRef<HTMLInputElement>(null);

  const [documents, setDocuments] = useState<SampleDocument[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [voidTarget, setVoidTarget] = useState<SampleDocument | null>(null);
  const [voidReason, setVoidReason] = useState("");

  const load = useCallback(() => {
    setLoading(true);
    ReceiveService.getSampleDocuments(sample.sampleId)
      .then((docs) => { setDocuments(docs); setError(null); })
      .catch((err) => setError(errorText(err, "Failed to load documents.")))
      .finally(() => setLoading(false));
  }, [sample.sampleId]);

  useEffect(load, [load]);

  const handleUpload = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      await ReceiveService.uploadSampleDocument(sample.sampleId, file);
      load();
    } catch (err) {
      setError(errorText(err, "Upload failed. Please try again."));
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = "";
    }
  };

  const handleView = async (doc: SampleDocument) => {
    try {
      const url = URL.createObjectURL(await ReceiveService.getSampleDocumentContent(doc.id));
      window.open(url, "_blank", "noopener,noreferrer");
      // The new tab has started loading it by then; free the blob after a minute.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      toast.error("Failed to retrieve document. Please try again.");
    }
  };

  const handleVoid = async () => {
    if (!voidTarget || !voidReason.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await ReceiveService.voidSampleDocument(voidTarget.id, voidReason.trim());
      setVoidTarget(null);
      setVoidReason("");
      load();
    } catch (err) {
      setError(errorText(err, "Void operation failed. Please try again."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <FloatingDialog
      open
      title={`Documents - ${sample.referenceNumber}`}
      onClose={() => { if (!busy) onClose(); }}
      maxWidth="sm"
      actions={
        <>
          {canChange && (
            <>
              <input
                ref={fileInput}
                type="file"
                hidden
                accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
                onChange={(e) => handleUpload(e.target.files?.[0])}
              />
              <Button
                startIcon={<UploadFileOutlinedIcon />}
                disabled={busy}
                onClick={() => fileInput.current?.click()}
                sx={{ mr: "auto", textTransform: "none", fontWeight: 600 }}
              >
                Attach supplier CoA
              </Button>
            </>
          )}
          <Button onClick={onClose} disabled={busy}>Close</Button>
        </>
      }
    >
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}><CircularProgress size={20} /></Box>
      ) : documents.length === 0 ? (
        <Typography sx={{ fontSize: 13, color: "text.secondary", fontStyle: "italic" }}>
          No documents have been attached to this sample.
        </Typography>
      ) : (
        <Stack divider={<Divider />}>
          {documents.map((doc) => (
            <Box key={doc.id} sx={{ py: 1.25, display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 1 }}>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexWrap: "wrap" }}>
                  <Typography
                    sx={{ fontSize: 13, fontWeight: 600, color: doc.status === "Voided" ? "text.disabled" : "text.primary", overflowWrap: "anywhere" }}
                  >
                    {doc.originalFileName}
                  </Typography>
                  <Chip
                    label={doc.status}
                    size="small"
                    color={doc.status === "Current" ? "success" : "error"}
                    variant={doc.status === "Current" ? "filled" : "outlined"}
                    sx={{ fontSize: 12, height: 20 }}
                  />
                </Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {doc.documentType === "SupplierCoa" ? "Supplier CoA" : "Other"} · {formatBytes(doc.fileSizeBytes)}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  Uploaded by {doc.uploadedByName} · {formatLabDate(doc.uploadedAt)}
                </Typography>
                {doc.status === "Voided" && doc.voidReason && (
                  <Typography sx={{ fontSize: 12, color: "error.main" }}>Voided: {doc.voidReason}</Typography>
                )}
              </Box>
              <Box sx={{ display: "flex", flexShrink: 0 }}>
                <Tooltip title="View / Download">
                  <IconButton aria-label="View document" size="small" onClick={() => handleView(doc)} sx={{ color: "primary.main" }}>
                    <VisibilityOutlinedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                {canVoid && doc.status === "Current" && (
                  <Tooltip title="Void">
                    <IconButton aria-label="Void document" size="small" disabled={busy} onClick={() => setVoidTarget(doc)} sx={{ color: "error.main" }}>
                      <BlockIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
              </Box>
            </Box>
          ))}
        </Stack>
      )}

      {voidTarget && (
        <Alert severity="warning" sx={{ mt: 2 }} icon={false}>
          <Typography sx={{ fontSize: 12, mb: 1 }}>
            <strong>{voidTarget.originalFileName}</strong> will be marked Voided. The file and its history are kept.
          </Typography>
          <TextField
            label="Reason for voiding *"
            size="small"
            fullWidth
            multiline
            rows={2}
            value={voidReason}
            onChange={(e) => setVoidReason(e.target.value)}
          />
          <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 1 }}>
            <Button size="small" disabled={busy} onClick={() => { setVoidTarget(null); setVoidReason(""); }}>Cancel</Button>
            <Button size="small" color="error" variant="contained" disabled={busy || !voidReason.trim()} onClick={handleVoid}>
              Void Document
            </Button>
          </Box>
        </Alert>
      )}
    </FloatingDialog>
  );
}
