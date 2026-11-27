import { useEffect, useState } from "react";
import { Box, IconButton, List, ListItem, ListItemText, MenuItem, TextField, Typography } from "@mui/material";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "../../../i18n";
import { FormDialog } from "../../../components/lab/FormDialog";
import { getErrorMessage } from "../../../utils/errorMessage";
import { getMySections, type LaboratorySection } from "../../../services/laboratorySectionService";
import { qualityService, type QualityCreatedDto } from "../../../services/qualityService";
import { LookupSelect, TEXT_MAX } from "../steps/stepKit";
import { useUserOptions } from "../steps/lookups";
import { AffectedPicker, type AffectedItem } from "./AffectedPicker";

const TITLE_MAX = 200;

interface ProposeChangeControlDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (created: QualityCreatedDto) => void | Promise<void>;
}

// Proposes a change control: what changes and why, in which laboratory, who owns it, and what it affects. The
// server checks that the owner is a Section Head or QC Manager of that laboratory.
export function ProposeChangeControlDialog({ open, onClose, onCreated }: ProposeChangeControlDialogProps) {
  const { t } = useTranslation("quality");
  const users = useUserOptions(open);
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [reason, setReason] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | "">("");
  const [affected, setAffected] = useState<AffectedItem[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return undefined;
    const controller = new AbortController();
    getMySections(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setSections(result);
      })
      .catch(() => {
        if (!controller.signal.aborted) setSections([]);
      });
    return () => controller.abort();
  }, [open]);

  const addAffected = (item: AffectedItem) =>
    setAffected((current) =>
      current.some((x) => x.targetType === item.targetType && x.targetId === item.targetId && x.newRecordCode === item.newRecordCode)
        ? current
        : [...current, item]
    );

  const submit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const created = await qualityService.proposeChangeControl({
        title: title.trim(),
        description: description.trim(),
        reason: reason.trim(),
        sectionId: sectionId === "" ? null : sectionId,
        affected: affected.map((item) => ({ targetType: item.targetType, targetId: item.targetId, newRecordCode: item.newRecordCode })),
        ownerUserId: ownerUserId === "" ? null : ownerUserId
      });
      setTitle("");
      setDescription("");
      setReason("");
      setSectionId("");
      setOwnerUserId("");
      setAffected([]);
      await onCreated(created);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("steps.changeControl.propose.failed")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={t("steps.changeControl.propose.title")}
      onClose={onClose}
      onSubmit={submit}
      submitLabel={t("steps.changeControl.propose.submit")}
      submitting={submitting}
      submitDisabled={!title.trim() || !description.trim() || !reason.trim()}
      error={error}
      maxWidth="md"
    >
      <TextField
        required
        label={t("steps.changeControl.propose.titleField")}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TITLE_MAX } }}
      />
      <TextField
        required
        multiline
        minRows={3}
        label={t("steps.changeControl.propose.description")}
        helperText={t("steps.changeControl.propose.descriptionHelp")}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
      />
      <TextField
        required
        multiline
        minRows={2}
        label={t("steps.changeControl.propose.reason")}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
      />
      <TextField
        select
        label={t("steps.changeControl.propose.section")}
        helperText={t("steps.changeControl.propose.sectionHelp")}
        value={sectionId}
        onChange={(e) => setSectionId(e.target.value === "" ? "" : Number(e.target.value))}
      >
        <MenuItem value="">{t("steps.changeControl.propose.wholeSite")}</MenuItem>
        {sections.map((section) => (
          <MenuItem key={section.sectionId} value={section.sectionId}>
            {section.sectionName}
          </MenuItem>
        ))}
      </TextField>
      <LookupSelect label={t("steps.changeControl.propose.owner")} value={ownerUserId} onChange={setOwnerUserId} lookup={users} />
      <Typography variant="body2" color="text.secondary" sx={{ mt: -1 }}>
        {t("steps.changeControl.propose.ownerHelp")}
      </Typography>

      <Box component="section" aria-label={t("steps.changeControl.propose.affectedTitle")}>
        <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
          {t("steps.changeControl.propose.affectedTitle")}
        </Typography>
        {affected.length > 0 && (
          <List dense disablePadding sx={{ mb: 1.5, border: 1, borderColor: "divider", borderRadius: 1 }}>
            {affected.map((item, index) => {
              const text = `${t(`steps.changeControl.affected.kinds.${item.targetType}`)}: ${item.label}`;
              return (
                <ListItem
                  key={`${item.targetType}-${item.targetId}-${item.newRecordCode}`}
                  secondaryAction={
                    <IconButton
                      edge="end"
                      aria-label={t("steps.changeControl.propose.removeAffected", { label: text })}
                      onClick={() => setAffected((current) => current.filter((_, i) => i !== index))}
                    >
                      <DeleteOutlinedIcon fontSize="small" />
                    </IconButton>
                  }
                >
                  <ListItemText primary={text} />
                </ListItem>
              );
            })}
          </List>
        )}
        <AffectedPicker onAdd={addAffected} />
      </Box>
    </FormDialog>
  );
}
