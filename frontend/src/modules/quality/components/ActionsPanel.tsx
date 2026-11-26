import { useState } from "react";
import { Box, Button, List, ListItem, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { FormDialog } from "../../../components/lab/FormDialog";
import { MetaBadge, ToneBadge } from "../../../components/StatusBadge";
import { serverMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import {
  qualityService,
  type QualityActionDetailDto,
  type QualityLinkDto,
  type QualityRecordType
} from "../../../services/qualityService";
import { PanelFrame } from "./PanelFrame";

const TEXT_MAX = 4000;

interface ActionsPanelProps {
  record: { id: number; type: QualityRecordType; version: number; isCancelled: boolean };
  actions: QualityActionDetailDto[];
  // The record's links, offered as the evidence link; null when they could not be loaded.
  links: QualityLinkDto[] | null;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
  onRequestExtension: (action: QualityActionDetailDto) => void;
}

// Only CAPA actions and change-control tasks are completed here: the other two types have no actions. Whether
// the signed-in person may complete or extend an action is decided by the server (it must be the responsible
// person, on a record in the right state); the buttons only follow that rule so a refusal is the exception.
export function ActionsPanel({ record, actions, links, currentUserId, onChanged, onRequestExtension }: ActionsPanelProps) {
  const { t } = useTranslation("quality");
  const [target, setTarget] = useState<QualityActionDetailDto | null>(null);
  const [evidence, setEvidence] = useState("");
  const [evidenceLinkId, setEvidenceLinkId] = useState<number | "">("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const completable = record.type === "Capa" || record.type === "ChangeControl";

  const closeDialog = () => {
    setTarget(null);
    setEvidence("");
    setEvidenceLinkId("");
    setError(null);
  };

  const submit = async () => {
    if (!target) return;
    setSubmitting(true);
    setError(null);
    try {
      const request = { evidence: evidence.trim(), evidenceLinkId: evidenceLinkId === "" ? null : evidenceLinkId };
      if (record.type === "Capa") await qualityService.completeCapaAction(target.id, request, record.version);
      else await qualityService.completeChangeControlTask(target.id, request, record.version);
      closeDialog();
      await onChanged();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("actions.completeFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PanelFrame title={t("actions.title")}>
      {actions.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("actions.empty")}
        </Typography>
      ) : (
        <List disablePadding>
          {actions.map((action) => {
            const done = action.doneAtUtc !== null;
            const mine = currentUserId !== null && action.responsibleUserId === currentUserId;
            const canAct = !done && mine && !record.isCancelled;
            return (
              <ListItem key={action.id} divider sx={{ display: "block", px: 0 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", mb: 0.5 }}>
                  <MetaBadge label={t(`actionKinds.${action.kind}`)} />
                  <ToneBadge tone={done ? "notDetected" : "info"} label={done ? t("actions.done") : t("actions.pending")} />
                  <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {t("actions.dueDate", { date: action.dueDate })}
                  </Typography>
                </Stack>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {action.description}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {t("actions.responsible")}: {action.responsibleName}
                </Typography>
                {done && (
                  <Box sx={{ mt: 0.5 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontVariantNumeric: "tabular-nums" }}>
                      {t("actions.completedLine", {
                        name: action.doneByName ?? "",
                        date: formatDateTimeWithTimeZone(action.doneAtUtc as string)
                      })}
                    </Typography>
                    {action.evidence && (
                      <Typography variant="body2" sx={{ mt: 0.25 }}>
                        {action.evidence}
                      </Typography>
                    )}
                  </Box>
                )}
                {canAct && (
                  <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                    {completable && (
                      <Button
                        size="small"
                        variant="contained"
                        aria-label={t("actions.completeAria", { description: action.description })}
                        onClick={() => setTarget(action)}
                      >
                        {t("actions.complete")}
                      </Button>
                    )}
                    <Button
                      size="small"
                      variant="outlined"
                      aria-label={t("actions.requestExtensionAria", { description: action.description })}
                      onClick={() => onRequestExtension(action)}
                    >
                      {t("actions.requestExtension")}
                    </Button>
                  </Stack>
                )}
              </ListItem>
            );
          })}
        </List>
      )}

      <FormDialog
        open={target !== null}
        title={t("actions.dialogTitle")}
        onClose={closeDialog}
        onSubmit={submit}
        submitLabel={t("actions.submit")}
        submitting={submitting}
        submitDisabled={!evidence.trim()}
        error={error}
      >
        <Typography variant="body2" color="text.secondary">
          {target?.description}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t("actions.dialogIntro")}
        </Typography>
        <TextField
          required
          multiline
          minRows={3}
          label={t("actions.evidenceLabel")}
          value={evidence}
          onChange={(e) => setEvidence(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        {links !== null && (
          <TextField
            select
            label={t("actions.evidenceLinkLabel")}
            value={evidenceLinkId}
            onChange={(e) => setEvidenceLinkId(e.target.value === "" ? "" : Number(e.target.value))}
          >
            <MenuItem value="">{t("actions.noEvidenceLink")}</MenuItem>
            {links.map((link) => (
              <MenuItem key={link.id} value={link.id}>
                {link.isRestricted || !link.label ? (link.number ?? link.id) : `${link.number ?? link.id} · ${link.label}`}
              </MenuItem>
            ))}
          </TextField>
        )}
      </FormDialog>
    </PanelFrame>
  );
}
