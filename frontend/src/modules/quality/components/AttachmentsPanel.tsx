import { useState } from "react";
import { Alert, Box, Button, List, ListItem, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { FileDropZone } from "../../../components/FileDropZone";
import { FormDialog } from "../../../components/lab/FormDialog";
import { ToneBadge } from "../../../components/StatusBadge";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { qualityService, type QualityAttachmentDto } from "../../../services/qualityService";
import { formatFileSize, saveBlob } from "../saveBlob";
import { PanelFrame } from "./PanelFrame";

// The server's allowlist (DiscussionAttachmentPolicy) decides what is accepted, by extension and by file
// signature; this only lets the browser's file picker offer the same types.
const ACCEPT = ".pdf,.png,.jpg,.jpeg,.gif,.doc,.docx,.xls,.xlsx,.pptx,.txt,.csv";
const REASON_MAX = 4000;

interface AttachmentsPanelProps {
  recordId: number;
  recordVersion: number;
  attachments: QualityAttachmentDto[];
  // Quality.Raise: upload. Without it files can still be downloaded.
  canEdit: boolean;
  // The record's owner or a Quality.Investigate holder (the server's rule for a destructive change): void.
  canRemove: boolean;
  onChanged: () => Promise<void>;
}

export function AttachmentsPanel({ recordId, recordVersion, attachments, canEdit, canRemove, onChanged }: AttachmentsPanelProps) {
  const { t } = useTranslation("quality");
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [panelError, setPanelError] = useState<string | null>(null);
  const [voidTarget, setVoidTarget] = useState<QualityAttachmentDto | null>(null);
  const [voidReason, setVoidReason] = useState("");
  const [voiding, setVoiding] = useState(false);
  const [voidError, setVoidError] = useState<string | null>(null);

  const upload = async () => {
    if (!file) return;
    setUploading(true);
    setPanelError(null);
    try {
      await qualityService.uploadAttachment(recordId, file, recordVersion);
      setFile(null);
      await onChanged();
    } catch (e: unknown) {
      setPanelError(getErrorMessage(e, t("attachments.uploadFailed")));
    } finally {
      setUploading(false);
    }
  };

  const download = async (attachment: QualityAttachmentDto) => {
    setPanelError(null);
    try {
      saveBlob(await qualityService.downloadAttachment(recordId, attachment.id), attachment.fileName);
    } catch (e: unknown) {
      setPanelError(getErrorMessage(e, t("attachments.downloadFailed")));
    }
  };

  const closeVoid = () => {
    setVoidTarget(null);
    setVoidReason("");
    setVoidError(null);
  };

  const submitVoid = async () => {
    if (!voidTarget) return;
    setVoiding(true);
    setVoidError(null);
    try {
      await qualityService.voidAttachment(recordId, voidTarget.id, voidReason.trim(), recordVersion);
      closeVoid();
      await onChanged();
    } catch (e: unknown) {
      setVoidError(getErrorMessage(e, t("attachments.voidFailed")));
    } finally {
      setVoiding(false);
    }
  };

  return (
    <PanelFrame title={t("attachments.title")}>
      <Stack spacing={1.5}>
        {panelError && (
          <Alert severity="error" role="alert">
            {panelError}
          </Alert>
        )}

        {attachments.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("attachments.empty")}
          </Typography>
        ) : (
          <List disablePadding>
            {attachments.map((attachment) => (
              <ListItem key={attachment.id} divider sx={{ display: "block", px: 0 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                  <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-all" }}>
                    {attachment.fileName}
                  </Typography>
                  {attachment.isAddendum && <ToneBadge tone="info" label={t("addendum.badge")} />}
                  {attachment.isVoided && <ToneBadge tone="detected" label={t("attachments.voided")} />}
                </Stack>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                  {t("attachments.uploadedLine", {
                    size: formatFileSize(attachment.size),
                    name: attachment.uploadedByName,
                    date: formatDateTimeWithTimeZone(attachment.uploadedAtUtc)
                  })}
                </Typography>
                {attachment.isVoided && attachment.voidedAtUtc && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                    {attachment.voidedByName
                      ? t("attachments.voidedLineWithName", {
                          name: attachment.voidedByName,
                          date: formatDateTimeWithTimeZone(attachment.voidedAtUtc),
                          reason: attachment.voidReason ?? ""
                        })
                      : t("attachments.voidedLine", {
                          date: formatDateTimeWithTimeZone(attachment.voidedAtUtc),
                          reason: attachment.voidReason ?? ""
                        })}
                  </Typography>
                )}
                <Stack direction="row" spacing={1} sx={{ mt: 0.75 }}>
                  <Button
                    size="small"
                    variant="outlined"
                    aria-label={t("attachments.downloadAria", { name: attachment.fileName })}
                    onClick={() => void download(attachment)}
                  >
                    {t("attachments.download")}
                  </Button>
                  {canRemove && !attachment.isVoided && (
                    <Button
                      size="small"
                      color="error"
                      aria-label={t("attachments.voidAria", { name: attachment.fileName })}
                      onClick={() => setVoidTarget(attachment)}
                    >
                      {t("attachments.void")}
                    </Button>
                  )}
                </Stack>
              </ListItem>
            ))}
          </List>
        )}

        {canEdit && (
          <Box>
            <FileDropZone
              file={file}
              onFile={(next) => {
                setFile(next);
                setPanelError(null);
              }}
              accept={ACCEPT}
              formatsHint={t("attachments.formats")}
              note={t("attachments.note")}
              disabled={uploading}
            />
            <Button
              sx={{ mt: 1 }}
              variant="contained"
              disabled={!file || uploading}
              onClick={() => void upload()}
            >
              {uploading ? t("attachments.uploading") : t("attachments.upload")}
            </Button>
          </Box>
        )}
      </Stack>

      <FormDialog
        open={voidTarget !== null}
        title={t("attachments.voidTitle")}
        onClose={closeVoid}
        onSubmit={submitVoid}
        submitLabel={t("attachments.voidSubmit")}
        submitting={voiding}
        submitDisabled={!voidReason.trim()}
        error={voidError}
      >
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {voidTarget?.fileName}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t("attachments.voidIntro")}
        </Typography>
        <TextField
          required
          multiline
          minRows={2}
          label={t("attachments.voidReason")}
          value={voidReason}
          onChange={(e) => setVoidReason(e.target.value)}
          slotProps={{ htmlInput: { maxLength: REASON_MAX } }}
        />
      </FormDialog>
    </PanelFrame>
  );
}
