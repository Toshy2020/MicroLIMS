import { useState } from "react";
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { FormDialog } from "../../components/lab";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useTranslation } from "../../i18n";
import {
  competencyService,
  type AnalystQualificationDto,
  type QualificationBasis
} from "../../services/competencyService";

export interface QualifyDialogProps {
  open: boolean;
  userId: number;
  analystName: string;
  testDefinitionId: number;
  testLabel: string;
  onClose: () => void;
  onQualified: (qualification: AnalystQualificationDto) => void;
}

const BASIS_OPTIONS: QualificationBasis[] = [
  "ObservedPractice",
  "ProficiencySample",
  "PreviousQualification",
  "Other"
];

function localTodayIso(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, "0");
  const d = String(now.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export function QualifyDialog({
  open,
  userId,
  analystName,
  testDefinitionId,
  testLabel,
  onClose,
  onQualified
}: QualifyDialogProps) {
  const { t } = useTranslation("competency");
  const [basis, setBasis] = useState<QualificationBasis>("ObservedPractice");
  const [evidence, setEvidence] = useState("");
  const [evidenceSampleId, setEvidenceSampleId] = useState("");
  const [qualifiedOn, setQualifiedOn] = useState(localTodayIso);
  const [attachments, setAttachments] = useState<File[]>([]);
  const [fieldErrors, setFieldErrors] = useState<{ evidence?: string; qualifiedOn?: string }>({});
  const [signingOpen, setSigningOpen] = useState(false);

  const resetAndClose = () => {
    setBasis("ObservedPractice");
    setEvidence("");
    setEvidenceSampleId("");
    setQualifiedOn(localTodayIso());
    setAttachments([]);
    setFieldErrors({});
    setSigningOpen(false);
    onClose();
  };

  const handleFormSubmit = () => {
    const nextErrors: { evidence?: string; qualifiedOn?: string } = {};
    if (!evidence.trim()) {
      nextErrors.evidence = t("qualify.validation.evidenceRequired");
    }
    if (!qualifiedOn) {
      nextErrors.qualifiedOn = t("qualify.validation.qualifiedOnRequired");
    } else if (qualifiedOn > localTodayIso()) {
      nextErrors.qualifiedOn = t("qualify.validation.qualifiedOnFuture");
    }
    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;
    setSigningOpen(true);
  };

  const handleSignConfirm = async (password: string) => {
    const parsedSampleId = evidenceSampleId.trim() ? Number(evidenceSampleId.trim()) : null;
    const created = await competencyService.qualify({
      userId,
      testDefinitionId,
      basis,
      evidence: evidence.trim(),
      evidenceSampleId: parsedSampleId && Number.isFinite(parsedSampleId) ? parsedSampleId : null,
      qualifiedOn,
      password,
      attachments: attachments.length > 0 ? attachments : undefined
    });
    toast.success(t("qualify.success"));
    onQualified(created);
    resetAndClose();
  };

  return (
    <>
      <FormDialog
        open={open && !signingOpen}
        title={t("qualify.title")}
        onClose={resetAndClose}
        onSubmit={handleFormSubmit}
        submitLabel={t("qualify.continueToSign")}
      >
        <Stack spacing={2}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5 }}>
            <Box>
              <Typography variant="caption" color="text.secondary">
                {t("qualify.analystLabel")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {analystName}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                {t("qualify.testLabel")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {testLabel}
              </Typography>
            </Box>
          </Box>

          <FormControl fullWidth size="small" required>
            <InputLabel id="qualify-basis-label">{t("qualify.basisLabel")}</InputLabel>
            <Select
              labelId="qualify-basis-label"
              label={t("qualify.basisLabel")}
              value={basis}
              onChange={(e) => setBasis(e.target.value as QualificationBasis)}
            >
              {BASIS_OPTIONS.map((opt) => (
                <MenuItem key={opt} value={opt}>
                  {t(`qualify.basis.${opt}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label={t("qualify.evidenceLabel")}
            placeholder={t("qualify.evidencePlaceholder")}
            value={evidence}
            onChange={(e) => setEvidence(e.target.value)}
            required
            multiline
            rows={3}
            fullWidth
            size="small"
            error={Boolean(fieldErrors.evidence)}
            helperText={fieldErrors.evidence}
          />

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5 }}>
            <TextField
              label={t("qualify.qualifiedOnLabel")}
              type="date"
              value={qualifiedOn}
              onChange={(e) => setQualifiedOn(e.target.value)}
              required
              fullWidth
              size="small"
              slotProps={{
                inputLabel: { shrink: true },
                htmlInput: { max: localTodayIso(), dir: "ltr" }
              }}
              error={Boolean(fieldErrors.qualifiedOn)}
              helperText={fieldErrors.qualifiedOn}
            />

            <TextField
              label={t("qualify.evidenceSampleIdLabel")}
              type="number"
              value={evidenceSampleId}
              onChange={(e) => setEvidenceSampleId(e.target.value)}
              fullWidth
              size="small"
              slotProps={{ htmlInput: { min: 1, dir: "ltr" } }}
            />
          </Box>

          <Box>
            <Button variant="outlined" component="label" size="small" sx={{ textTransform: "none" }}>
              {t("qualify.attachmentsLabel")}
              <input
                hidden
                type="file"
                multiple
                aria-label={t("qualify.attachmentsLabel")}
                onChange={(e) => {
                  const files = e.target.files ? Array.from(e.target.files) : [];
                  setAttachments(files);
                }}
              />
            </Button>
            {attachments.length > 0 && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                {attachments.map((f) => f.name).join(", ")}
              </Typography>
            )}
          </Box>
        </Stack>
      </FormDialog>

      <SignatureDialog
        open={signingOpen}
        meaningStatement={t("qualify.meaningStatement", { lng: "en" })}
        meaningStatementTranslation={t("qualify.meaningStatementAr")}
        onCancel={() => setSigningOpen(false)}
        onConfirm={handleSignConfirm}
      />
    </>
  );
}
