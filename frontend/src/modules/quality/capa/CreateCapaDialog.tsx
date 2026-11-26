import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { FormDialog } from "../../../components/lab/FormDialog";
import { getErrorMessage } from "../../../utils/errorMessage";
import { qualityService, type QualityCreatedDto } from "../../../services/qualityService";
import { LookupSelect, TEXT_MAX } from "../steps/stepKit";
import { useUserOptions } from "../steps/lookups";

const TITLE_MAX = 200;

export interface CapaSourceRecord {
  id: number;
  number: string;
  sectionId: number | null;
}

interface CreateCapaDialogProps {
  open: boolean;
  // The record the CAPA is raised from: a deviation, or the not-effective CAPA this one follows.
  source: CapaSourceRecord;
  onClose: () => void;
  onCreated: (created: QualityCreatedDto) => void | Promise<void>;
}

// Raises a CAPA from another quality record. The new CAPA starts in the plan step with the chosen owner.
export function CreateCapaDialog({ open, source, onClose, onCreated }: CreateCapaDialogProps) {
  const { t } = useTranslation("quality");
  const users = useUserOptions(open);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [ownerUserId, setOwnerUserId] = useState<number | "">("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (ownerUserId === "") return;
    setSubmitting(true);
    setError(null);
    try {
      const created = await qualityService.createCapa({
        title: title.trim(),
        description: description.trim(),
        sectionId: source.sectionId,
        sourceRecordId: source.id,
        links: null,
        ownerUserId
      });
      setTitle("");
      setDescription("");
      setOwnerUserId("");
      await onCreated(created);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("steps.capa.create.failed")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={t("steps.capa.create.title")}
      onClose={onClose}
      onSubmit={submit}
      submitLabel={t("steps.capa.create.submit")}
      submitting={submitting}
      submitDisabled={!title.trim() || !description.trim() || ownerUserId === ""}
      error={error}
    >
      <Typography variant="body2" color="text.secondary">
        {t("steps.capa.create.from", { number: source.number })}
      </Typography>
      <TextField
        required
        label={t("steps.capa.create.titleField")}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TITLE_MAX } }}
      />
      <TextField
        required
        multiline
        minRows={3}
        label={t("steps.capa.create.description")}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
      />
      <LookupSelect required label={t("steps.capa.create.owner")} value={ownerUserId} onChange={setOwnerUserId} lookup={users} />
    </FormDialog>
  );
}
