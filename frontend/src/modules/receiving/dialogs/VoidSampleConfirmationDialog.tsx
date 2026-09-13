import { useState } from "react";
import {
  Button,
  Typography,
  Box,
  Alert,
  TextField,
  Stack,
  useTheme
} from "@mui/material";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { SampleRecord } from "../types/receivingTypes";
import { CategoryBadge } from "../../../components/StatusBadge";
import { ReceiveService } from "../services/ReceiveService";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";

interface Props {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onSuccess: () => void;
}

// The statement the backend records for SignatureMeaning.SampleVoided.
const MEANING_STATEMENT = "I void this sample record; it is struck from the register.";

export function VoidSampleConfirmationDialog({ open, sample, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [signing, setSigning] = useState(false);

  if (!sample) return null;

  const handleConfirm = () => {
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }
    setSigning(true);
  };

  // A failed signature or a refusal is thrown back to the signature dialog,
  // which shows the server's message and stays open.
  const handleSign = async (password: string) => {
    await ReceiveService.voidSample(sample.sampleId, reason.trim(), password);
    setSigning(false);
    setReason("");
    onSuccess();
  };

  const handleCancel = () => {
    setReason("");
    setReasonError(false);
    setSigning(false);
    onClose();
  };

  return (
    <>
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
                Strikes the sample from the register - signed with your password
              </Typography>
            </Box>
          </>
        }
        actions={
          <>
            <Button onClick={handleCancel} color="inherit">
              Cancel
            </Button>
            <Button
              onClick={handleConfirm}
              disabled={!reason.trim()}
              variant="contained"
              color="error"
              startIcon={<BlockOutlinedIcon />}
              sx={{
                fontWeight: 700,
                textTransform: "none",
                px: 2.5
              }}
            >
              Sign &amp; Void
            </Button>
          </>
        }
      >
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
            The sample record and its open tests will be marked as <strong>Voided</strong>.
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
            helperText={reasonError ? "A reason for voiding is required." : "Recorded with your electronic signature and in the sample's audit trail."}
          />
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={MEANING_STATEMENT}
        onCancel={() => setSigning(false)}
        onConfirm={handleSign}
      />
    </>
  );
}
