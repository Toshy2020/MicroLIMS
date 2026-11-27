import { useEffect, useState } from "react";
import { Box, Button, MenuItem, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { FormDialog } from "../../../components/lab/FormDialog";
import { getErrorMessage } from "../../../utils/errorMessage";
import { getMySections, type LaboratorySection } from "../../../services/laboratorySectionService";
import { qualityService, type QualityCreatedDto } from "../../../services/qualityService";
import { RecordPicker } from "../steps/RecordPicker";
import { TEXT_MAX, todayIsoDate } from "../steps/stepKit";

const TITLE_MAX = 200;
const SHORT_MAX = 200;

interface ReceiveComplaintDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (created: QualityCreatedDto) => void | Promise<void>;
}

// QA records a received complaint: where it came from, when, the external reference, the product (chosen with the
// search, never typed) and lot where known, and the complaint text.
export function ReceiveComplaintDialog({ open, onClose, onCreated }: ReceiveComplaintDialogProps) {
  const { t } = useTranslation("quality");
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [title, setTitle] = useState("");
  const [source, setSource] = useState("");
  const [receivedOn, setReceivedOn] = useState(todayIsoDate());
  const [externalReference, setExternalReference] = useState("");
  const [item, setItem] = useState<{ id: number; name: string } | null>(null);
  const [lotText, setLotText] = useState("");
  const [complaintText, setComplaintText] = useState("");
  const [sectionId, setSectionId] = useState<number | "">("");
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

  const submit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const created = await qualityService.receiveComplaint({
        title: title.trim(),
        source: source.trim(),
        receivedOn,
        externalReference: externalReference.trim() || null,
        itemId: item?.id ?? null,
        lotText: lotText.trim() || null,
        complaintText: complaintText.trim(),
        sectionId: sectionId === "" ? null : sectionId
      });
      setTitle("");
      setSource("");
      setReceivedOn(todayIsoDate());
      setExternalReference("");
      setItem(null);
      setLotText("");
      setComplaintText("");
      setSectionId("");
      await onCreated(created);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("steps.complaint.receive.failed")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={t("steps.complaint.receive.title")}
      onClose={onClose}
      onSubmit={submit}
      submitLabel={t("steps.complaint.receive.submit")}
      submitting={submitting}
      submitDisabled={!title.trim() || !source.trim() || !receivedOn || !complaintText.trim()}
      error={error}
      maxWidth="md"
    >
      <TextField
        required
        label={t("steps.complaint.receive.titleField")}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TITLE_MAX } }}
      />
      <TextField
        required
        label={t("steps.complaint.receive.source")}
        helperText={t("steps.complaint.receive.sourceHelp")}
        value={source}
        onChange={(e) => setSource(e.target.value)}
        slotProps={{ htmlInput: { maxLength: SHORT_MAX } }}
      />
      <TextField
        required
        type="date"
        label={t("steps.complaint.receive.receivedOn")}
        value={receivedOn}
        onChange={(e) => setReceivedOn(e.target.value)}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <TextField
        label={t("steps.complaint.receive.externalReference")}
        value={externalReference}
        onChange={(e) => setExternalReference(e.target.value)}
        slotProps={{ htmlInput: { maxLength: SHORT_MAX } }}
      />

      <Box component="section" aria-label={t("steps.complaint.receive.product")}>
        <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
          {t("steps.complaint.receive.product")}
        </Typography>
        {item ? (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Typography variant="body2">{t("steps.complaint.receive.itemChosen", { name: item.name })}</Typography>
            <Button size="small" onClick={() => setItem(null)}>
              {t("steps.complaint.receive.itemClear")}
            </Button>
          </Box>
        ) : (
          <RecordPicker
            label={t("steps.complaint.receive.itemSearch")}
            entityTypes={["Item"]}
            onPick={(hit) => setItem({ id: hit.id, name: hit.title })}
          />
        )}
      </Box>

      <TextField
        label={t("steps.complaint.receive.lot")}
        helperText={t("steps.complaint.receive.lotHelp")}
        value={lotText}
        onChange={(e) => setLotText(e.target.value)}
        slotProps={{ htmlInput: { maxLength: SHORT_MAX } }}
      />
      <TextField
        required
        multiline
        minRows={3}
        label={t("steps.complaint.receive.text")}
        value={complaintText}
        onChange={(e) => setComplaintText(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
      />
      <TextField
        select
        label={t("steps.complaint.receive.section")}
        value={sectionId}
        onChange={(e) => setSectionId(e.target.value === "" ? "" : Number(e.target.value))}
      >
        <MenuItem value="">{t("steps.complaint.receive.wholeSite")}</MenuItem>
        {sections.map((section) => (
          <MenuItem key={section.sectionId} value={section.sectionId}>
            {section.sectionName}
          </MenuItem>
        ))}
      </TextField>
    </FormDialog>
  );
}
