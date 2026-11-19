import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  List,
  ListItem,
  Typography
} from "@mui/material";
import ChecklistOutlinedIcon from "@mui/icons-material/ChecklistOutlined";
import { RetestEligibleTest, SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";

interface Props {
  open: boolean;
  sample: SampleRecord | null;
  // Section Heads and Supervisors choose; everyone else sees the list only.
  // The server enforces the role and the laboratory scope either way.
  canSelect: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

// The statement the backend records for SignatureMeaning.TestsSelected.
const MEANING_STATEMENT = "I choose these tests for this periodic retest.";

export function SelectRetestTestsDialog({ open, sample, canSelect, onClose, onSuccess }: Props) {
  const { t } = useTranslation("receiving");
  const [tests, setTests] = useState<RetestEligibleTest[] | null>(null);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [loadError, setLoadError] = useState<string | null>(null);
  const [signing, setSigning] = useState(false);
  const sampleId = sample?.sampleId;

  useEffect(() => {
    if (!open || sampleId == null) return;
    let cancelled = false;
    setTests(null);
    setChosen(new Set());
    setLoadError(null);
    ReceiveService.getTestSelection(sampleId)
      .then((list) => {
        if (!cancelled) setTests(list);
      })
      .catch(() => {
        if (!cancelled) setLoadError(t("selectRetest.loadFailed"));
      });
    return () => {
      cancelled = true;
    };
  }, [open, sampleId, t]);

  if (!sample) return null;

  const toggle = (code: string) =>
    setChosen((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });

  // A failed signature or a refusal is thrown back to the signature dialog,
  // which shows the server's message and stays open.
  const handleSign = async (password: string) => {
    await ReceiveService.selectTests(sample.sampleId, Array.from(chosen), password);
    setSigning(false);
    onSuccess();
  };

  const handleClose = () => {
    setSigning(false);
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open}
        onClose={handleClose}
        maxWidth="sm"
        title={
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
            <ChecklistOutlinedIcon />
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 700 }}>{t("selectRetest.title")}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Ltr>{sample.referenceNumber}</Ltr> · {sample.displayName}
                {sample.grade ? t("selectRetest.grade", { grade: sample.grade }) : ""}
              </Typography>
            </Box>
          </Box>
        }
        actions={
          <>
            <Button onClick={handleClose} color="inherit">
              {canSelect && tests?.some((test) => test.canSelect) ? t("selectRetest.cancel") : t("selectRetest.close")}
            </Button>
            {canSelect && tests?.some((test) => test.canSelect) && (
              <Button
                variant="contained"
                disabled={chosen.size === 0}
                onClick={() => setSigning(true)}
                sx={{ fontWeight: 700, textTransform: "none", px: 2.5 }}
              >
                {t("selectRetest.signAndSelect")}
              </Button>
            )}
          </>
        }
      >
        {!(canSelect && tests?.some((test) => test.canSelect)) && tests !== null && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {t("selectRetest.info")}
          </Alert>
        )}
        {loadError && <Alert severity="error">{loadError}</Alert>}
        {!loadError && tests === null && <CircularProgress size={24} />}
        {tests !== null && tests.length === 0 && (
          <Alert severity="warning">{t("selectRetest.empty")}</Alert>
        )}
        {tests !== null && tests.length > 0 && (
          <List dense disablePadding>
            {tests.map((test) => (
              <ListItem key={test.testCode} disableGutters>
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={chosen.has(test.testCode)}
                      disabled={!canSelect || !test.canSelect}
                      onChange={() => toggle(test.testCode)}
                    />
                  }
                  label={
                    <Box>
                      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>
                        <Ltr>{test.testCode}</Ltr> {test.displayName && test.displayName !== test.testCode ? `· ${test.displayName}` : ""}
                      </Typography>
                      <Typography sx={{ fontSize: 11.5, color: "text.secondary" }}>{test.sectionName}</Typography>
                    </Box>
                  }
                />
              </ListItem>
            ))}
          </List>
        )}
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={MEANING_STATEMENT}
        meaningStatementTranslation={t("selectRetest.signatureMeaning")}
        onCancel={() => setSigning(false)}
        onConfirm={handleSign}
      />
    </>
  );
}
