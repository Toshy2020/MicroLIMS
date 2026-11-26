import { useState } from "react";
import { Box, IconButton, List, ListItem, ListItemText, TextField, Typography } from "@mui/material";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";
import { RecordPicker } from "../steps/RecordPicker";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, useStepRunner } from "../steps/stepKit";

interface ComplaintInvestigationFormProps {
  record: QualityRecordDetailDto;
  permissions: QualityTypeActionsProps["permissions"];
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

interface PickedSample {
  id: number;
  title: string;
}

// The owner records the findings and links the complaint samples (received through the normal receiving process);
// a Section Head other than the owner signs the laboratory report once the findings are saved.
export function ComplaintInvestigationForm({ record, permissions, currentUserId, onChanged }: ComplaintInvestigationFormProps) {
  const { t } = useTranslation("quality");
  const detail = record.complaint;
  const [findings, setFindings] = useState(detail?.findings ?? "");
  const [samples, setSamples] = useState<PickedSample[]>([]);
  const [signing, setSigning] = useState(false);
  const saver = useStepRunner(onChanged);

  const isOwner = currentUserId !== null && record.ownerUserId === currentUserId;
  const canSave = !permissions.approve && (isOwner || permissions.investigate);
  const saved = Boolean(detail?.findings?.trim());
  const signLock = isOwner
    ? t("steps.complaint.investigation.ownerCannotSign")
    : !saved
      ? t("steps.complaint.investigation.saveFirst")
      : null;

  const save = () =>
    saver.run(
      () =>
        qualityService.saveComplaintInvestigation(
          record.id,
          { findings: findings.trim(), sampleIds: samples.length > 0 ? samples.map((sample) => sample.id) : null },
          record.version
        ),
      t("steps.complaint.investigation.saveFailed")
    );

  return (
    <PanelFrame title={t("steps.complaint.investigation.title")}>
      <StepForm
        error={saver.error}
        actions={
          <>
            {canSave && (
              <GatedButton
                label={t("steps.complaint.investigation.save")}
                lockedReason={findings.trim() ? null : t("steps.complaint.investigation.needsFindings")}
                busy={saver.busy}
                onClick={() => void save()}
              />
            )}
            {permissions.labConclude && (
              <GatedButton
                label={t("steps.complaint.investigation.sign")}
                variant="outlined"
                lockedReason={signLock}
                onClick={() => setSigning(true)}
              />
            )}
          </>
        }
      >
        {detail?.whatToTest && (
          <Typography variant="body2">{t("steps.complaint.investigation.whatToTest", { text: detail.whatToTest })}</Typography>
        )}
        {!canSave && !permissions.labConclude && (
          <Typography variant="body2" color="text.secondary">
            {t("steps.complaint.investigation.withOwner")}
          </Typography>
        )}
        <TextField
          required
          multiline
          minRows={3}
          label={t("steps.complaint.investigation.findings")}
          value={findings}
          disabled={!canSave}
          onChange={(e) => setFindings(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        {canSave && (
          <Box component="section" aria-label={t("steps.complaint.investigation.samples")}>
            <Typography variant="subtitle2" component="h3">
              {t("steps.complaint.investigation.samples")}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
              {t("steps.complaint.investigation.samplesHelp")}
            </Typography>
            {samples.length > 0 && (
              <List dense disablePadding sx={{ mb: 1.5, border: 1, borderColor: "divider", borderRadius: 1 }}>
                {samples.map((sample) => (
                  <ListItem
                    key={sample.id}
                    secondaryAction={
                      <IconButton
                        edge="end"
                        aria-label={t("steps.complaint.investigation.removeSample", { title: sample.title })}
                        onClick={() => setSamples((current) => current.filter((x) => x.id !== sample.id))}
                      >
                        <DeleteOutlinedIcon fontSize="small" />
                      </IconButton>
                    }
                  >
                    <ListItemText primary={sample.title} />
                  </ListItem>
                ))}
              </List>
            )}
            <RecordPicker
              label={t("steps.complaint.investigation.sampleSearch")}
              entityTypes={["Sample"]}
              onPick={(hit) =>
                setSamples((current) => (current.some((x) => x.id === hit.id) ? current : [...current, { id: hit.id, title: hit.title }]))
              }
            />
          </Box>
        )}
      </StepForm>
      <SignStepDialog
        open={signing}
        meaning="QualityLabConclusion"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          await qualityService.signComplaintLabReport(record.id, { password }, record.version);
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
