import { useEffect, useState } from "react";
import { Button, Typography, Box, TextField, useTheme } from "@mui/material";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { SampleSummaryService } from "../testingWorkspace/services/SampleSummaryService";

interface Props {
  open: boolean;
  sampleId: number | null;
  // The laboratory whose still-open tests this closes - the Section
  // Head's own lab, resolved by the caller from the summary's sections.
  sectionId: number | null;
  sectionName?: string | null;
  // Whichever lab already rejected the sample - seeds the default reason
  // text (design.md §5.3: "Sample rejected by <lab>"). Left blank if the
  // caller couldn't determine it; the field stays required either way.
  rejectingLabName?: string | null;
  onClose: () => void;
  onSuccess: () => void;
}

// Signed: closes every still-open test of the caller's own laboratory
// after another laboratory has already rejected the sample (design.md
// §5.3). Follows the same two-step reason-then-signature pattern as
// VoidSampleConfirmationDialog - a required reason here, then a
// SignatureDialog that re-verifies the password server-side and surfaces
// the server's error text on failure.
export function CloseTestingDialog({ open, sampleId, sectionId, sectionName, rejectingLabName, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);

  // Re-seed the default reason text each time the dialog is opened for a
  // (possibly different) sample/rejecting lab, but never overwrite what
  // the Section Head has already typed while it stays open.
  useEffect(() => {
    if (open) {
      setReason(rejectingLabName ? `Sample rejected by ${rejectingLabName}` : "");
    }
  }, [open, rejectingLabName]);

  if (!sampleId || !sectionId) return null;

  const handleConfirm = () => {
    if (!reason.trim()) return;
    setSigning(true);
  };

  // A failed signature or a server refusal is thrown back to the
  // signature dialog, which shows the server's message and stays open.
  const handleSign = async (password: string) => {
    await SampleSummaryService.closeTesting(sampleId, sectionId, password, reason.trim());
    setSigning(false);
    setReason("");
    onSuccess();
  };

  const handleCancel = () => {
    setReason("");
    setSigning(false);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signing}
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
                Close Testing{sectionName ? ` — ${sectionName}` : ""}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                Stops your laboratory's remaining tests on this sample - signed with your password
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
              sx={{ fontWeight: 700, textTransform: "none", px: 2.5 }}
            >
              Sign &amp; Close
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          Every still-open test in this laboratory will be recorded as Cancelled at the stage it had
          reached. Approved results already on file are kept.
        </Typography>
        <TextField
          fullWidth
          required
          label="Reason"
          multiline
          rows={2}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          autoFocus
        />
      </FloatingDialog>

      <SignatureDialog
        open={open && signing}
        meaningStatement="I am closing this laboratory's testing; it will not be resumed on this sample."
        onCancel={() => setSigning(false)}
        onConfirm={handleSign}
      />
    </>
  );
}
