import { useEffect, useState } from "react";
import {
  Button,
  Typography,
  Box,
  Alert,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  CircularProgress,
  Stack,
  useTheme
} from "@mui/material";
import LibraryAddOutlinedIcon from "@mui/icons-material/LibraryAddOutlined";
import { SampleRecord, ReceiptLabOption } from "../types/receivingTypes";
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

// The statement the backend records for this signed action (design 3.3).
const MEANING_STATEMENT = "I add this laboratory to the sample; it creates that lab's test orders.";

// Signed action: adds a second laboratory's tests to a sample already
// received, without re-entering the sample's details (design 3.3). The
// choices offered are every lab the item has tests for, minus whichever
// labs already have a TestOrder on this sample.
export function AddLaboratoryDialog({ open, sample, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const [options, setOptions] = useState<ReceiptLabOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [sectionId, setSectionId] = useState<number | "">("");
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [signing, setSigning] = useState(false);

  useEffect(() => {
    if (!open || !sample) return;
    setReason("");
    setReasonError(false);
    setSectionId("");
    setLoadError(null);

    if (!sample.itemId) {
      setOptions([]);
      return;
    }

    setLoading(true);
    const existingSectionIds = new Set(sample.assignedTests.map((t) => t.sectionId));
    ReceiveService.receiptLabs(sample.itemId)
      .then((labs) => setOptions(labs.filter((l) => l.testCount > 0 && !existingSectionIds.has(l.sectionId))))
      .catch((err) => setLoadError(err?.response?.data?.message || "Unable to load available laboratories."))
      .finally(() => setLoading(false));
  }, [open, sample]);

  if (!sample) return null;

  const handleConfirm = () => {
    if (!sectionId) return;
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }
    setSigning(true);
  };

  const handleSign = async (password: string) => {
    if (!sectionId) return;
    await ReceiveService.addLaboratory(sample.sampleId, Number(sectionId), reason.trim(), password);
    setSigning(false);
    setReason("");
    setSectionId("");
    onSuccess();
  };

  const handleCancel = () => {
    setReason("");
    setReasonError(false);
    setSectionId("");
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
                bgcolor: theme.custom.status.purple.bg,
                color: theme.custom.status.purple.text
              }}
            >
              <LibraryAddOutlinedIcon sx={{ fontSize: 22 }} />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
                Add Laboratory
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                Creates that lab's test orders on this sample - signed with your password
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
              disabled={!sectionId || !reason.trim()}
              variant="contained"
              color="primary"
              startIcon={<LibraryAddOutlinedIcon />}
              sx={{ fontWeight: 700, textTransform: "none", px: 2.5 }}
            >
              Sign &amp; Add
            </Button>
          </>
        }
      >
        {loadError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {loadError}
          </Alert>
        )}

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
                Item / Type:
              </Typography>
              <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {sample.displayName}
                </Typography>
                <CategoryBadge category={sample.category} />
              </Box>
            </Box>
          </Stack>
        </Box>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : options.length === 0 ? (
          <Alert severity="info" sx={{ mb: 2 }}>
            No other laboratory has assigned tests for this item, or every laboratory with tests is already on this sample.
          </Alert>
        ) : (
          <FormControl fullWidth size="small" sx={{ mb: 2.5 }}>
            <InputLabel id="add-lab-select-label">Laboratory</InputLabel>
            <Select
              labelId="add-lab-select-label"
              label="Laboratory"
              value={sectionId}
              onChange={(e) => setSectionId(e.target.value as number)}
            >
              {options.map((o) => (
                <MenuItem key={o.sectionId} value={o.sectionId}>
                  {o.sectionName} ({o.testCount} test{o.testCount === 1 ? "" : "s"})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        <TextField
          label="Reason *"
          placeholder="Why this laboratory is being added now..."
          multiline
          rows={2}
          fullWidth
          size="small"
          value={reason}
          onChange={(e) => {
            setReason(e.target.value);
            if (reasonError) setReasonError(false);
          }}
          error={reasonError}
          helperText={reasonError ? "A reason is required." : "Recorded with your electronic signature and in the sample's audit trail."}
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
