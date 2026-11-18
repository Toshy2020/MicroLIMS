import { useRef, useState } from "react";
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
import { Trans } from "react-i18next";
import { SampleRecord } from "../types/receivingTypes";
import { CategoryBadge } from "../../../components/StatusBadge";
import { ReceiveService } from "../services/ReceiveService";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";

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
  const { t } = useTranslation("receiving");
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [signing, setSigning] = useState(false);
  const cancelRef = useRef<HTMLButtonElement>(null);

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
        // Voiding is destructive: focus starts on Cancel, so an accidental Enter never opens the signature step.
        onEntered={() => cancelRef.current?.focus()}
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
                {t("voidSample.title")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("voidSample.subtitle")}
              </Typography>
            </Box>
          </>
        }
        actions={
          <>
            {!reason.trim() && (
              <Typography role="status" sx={{ fontSize: 12, color: "text.secondary", me: "auto", ps: 1 }}>
                {t("voidSample.enterReasonHint")}
              </Typography>
            )}
            <Button ref={cancelRef} autoFocus onClick={handleCancel} color="inherit">
              {t("voidSample.cancel")}
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
              {t("voidSample.signAndVoid")}
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
            <Trans
              i18nKey="voidSample.warning"
              ns="receiving"
              values={{ name: sample.displayName, sampleId: sample.sampleId }}
              components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
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
                  {t("voidSample.sampleReference")}
                </Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
                  <Ltr>{sample.referenceNumber}</Ltr> (<Ltr>#{sample.sampleId}</Ltr>)
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                  {t("voidSample.itemLocation")}
                </Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                  {sample.displayName}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                  {t("voidSample.itemType")}
                </Typography>
                <CategoryBadge category={sample.category} />
              </Box>

              {(sample.batchNumber || sample.controlNumber) && (
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                    {t("voidSample.batchControl")}
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.primary" }}>
                    {sample.batchNumber && (
                      <>
                        {t("register.batchPrefix")} <Ltr>{sample.batchNumber}</Ltr>
                      </>
                    )}
                    {sample.batchNumber && sample.controlNumber ? " · " : ""}
                    {sample.controlNumber && (
                      <>
                        {t("register.controlPrefix")} <Ltr>{sample.controlNumber}</Ltr>
                      </>
                    )}
                  </Typography>
                </Box>
              )}
            </Stack>
          </Box>

          <TextField
            label={t("voidSample.reasonLabel")}
            placeholder={t("voidSample.reasonPlaceholder")}
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
            helperText={reasonError ? t("voidSample.reasonRequired") : t("voidSample.reasonHelper")}
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
