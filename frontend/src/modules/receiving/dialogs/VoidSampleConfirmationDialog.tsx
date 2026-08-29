import React, { useState } from "react";
import {
  Button,
  Typography,
  Box,
  Alert,
  TextField,
  LinearProgress,
  Stack,
  useTheme
} from "@mui/material";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { SampleRecord } from "../types/receivingTypes";
import { CategoryBadge } from "../../../components/StatusBadge";
import { ReceiveService } from "../services/ReceiveService";
import { FloatingDialog } from "../../../components/FloatingDialog";

interface Props {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function VoidSampleConfirmationDialog({ open, sample, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!sample) return null;

  const handleConfirm = async () => {
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await ReceiveService.voidSample(sample.sampleId, reason.trim());
      setReason("");
      onSuccess();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to void sample. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    if (loading) return;
    setReason("");
    setReasonError(false);
    setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      onClose={handleCancel}
      maxWidth="sm"
      paperSx={{ borderRadius: 2.5, p: 1 }}
      titleSx={{ display: "flex", alignItems: "center", gap: 1.25, pb: 1 }}
      title={
        <>
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              width: 36,
              height: 36,
              borderRadius: 2,
              bgcolor: theme.custom.status.detected.bg,
              color: theme.custom.status.detected.text
            }}
          >
            <BlockOutlinedIcon sx={{ fontSize: 22 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
              Void Sample Confirmation
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Confirm marking sample as voided / cancelled
            </Typography>
          </Box>
        </>
      }
      subHeader={loading && <LinearProgress sx={{ mb: 1.5 }} />}
      actions={
        <>
          <Button onClick={handleCancel} disabled={loading} color="inherit">
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={loading || !reason.trim()}
            variant="contained"
            color="error"
            startIcon={<BlockOutlinedIcon />}
            sx={{
              fontWeight: 700,
              textTransform: "none",
              px: 2.5
            }}
          >
            {loading ? "Voiding..." : "Confirm Void"}
          </Button>
        </>
      }
    >
        {error && (
          <Alert severity="error" sx={{ mb: 2, fontSize: 12.5 }}>
            {error}
          </Alert>
        )}

        <Alert
          severity="warning"
          icon={<WarningAmberOutlinedIcon />}
          sx={{
            mb: 2.5,
            fontSize: 12.5,
            border: "1px solid",
            borderColor: "warning.light"
          }}
        >
          Are you sure you want to void <strong>{sample.displayName}</strong> (Sample #{sample.sampleId})?
          This action will mark the sample record as <strong>Voided / Cancelled</strong>.
        </Alert>

        {/* Sample Summary Information Box */}
        <Box
          sx={{
            p: 1.75,
            mb: 2.5,
            borderRadius: 2,
            bgcolor: "background.default",
            border: "1px solid",
            borderColor: "divider"
          }}
        >
          <Stack spacing={1}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                Sample Reference:
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
                {sample.referenceNumber} (#{sample.sampleId})
              </Typography>
            </Box>

            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                Item / Location:
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                {sample.displayName}
              </Typography>
            </Box>

            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                Item Type:
              </Typography>
              <CategoryBadge category={sample.category} />
            </Box>

            {(sample.batchNumber || sample.controlNumber) && (
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                  Batch / Control No.:
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                  {sample.batchNumber ? `B: ${sample.batchNumber}` : ""}
                  {sample.batchNumber && sample.controlNumber ? " · " : ""}
                  {sample.controlNumber ? `C: ${sample.controlNumber}` : ""}
                </Typography>
              </Box>
            )}
          </Stack>
        </Box>

        <TextField
          autoFocus
          label="Reason for Voiding *"
          placeholder="Please provide the operational or compliance reason for voiding this sample..."
          multiline
          rows={3}
          fullWidth
          size="small"
          value={reason}
          onChange={(e) => {
            setReason(e.target.value);
            if (reasonError) setReasonError(false);
          }}
          error={reasonError}
          helperText={reasonError ? "A reason for voiding is required." : undefined}
        />
    </FloatingDialog>
  );
}
