import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  FormControl,
  FormControlLabel,
  Grid,
  Paper,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../i18n";
import type {
  OosChecklistAnswer,
  OosChecklistFactsDto,
  OosChecklistTemplateItemDto,
  OosInvestigationEntryDto,
  OosInvestigationResultDto
} from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { serverMessage } from "../../../utils/errorMessage";
import { FactsPanel } from "./FactsPanel";

const CHECKLIST_ANSWERS: OosChecklistAnswer[] = ["Yes", "No", "NotApplicable"];

interface ChecklistPanelProps {
  items: OosChecklistTemplateItemDto[];
  entries: OosInvestigationEntryDto[];
  facts: OosChecklistFactsDto | null;
  results: OosInvestigationResultDto[];
  factsLoading: boolean;
  canEdit: boolean;
  version: number;
  onAnswer: (
    itemKey: string,
    answer: OosChecklistAnswer,
    comment: string | null,
    version: number
  ) => Promise<string | null | void>;
}

interface ItemDraft {
  answer: OosChecklistAnswer | "";
  comment: string;
  saving: boolean;
  error?: string | null;
}

export function ChecklistPanel({
  items,
  entries,
  facts,
  results,
  factsLoading,
  canEdit,
  version,
  onAnswer
}: ChecklistPanelProps) {
  const { t } = useTranslation("oosTracking");
  const sortedItems = [...items].sort((a, b) => a.order - b.order);

  const latestByKey = new Map<string, OosInvestigationEntryDto>();
  for (const entry of entries) {
    if (entry.kind === "ChecklistAnswer" && entry.checklistItemKey) {
      latestByKey.set(entry.checklistItemKey, entry);
    }
  }

  const [drafts, setDrafts] = useState<Record<string, ItemDraft>>({});

  const baseDraft = (key: string): ItemDraft => {
    const latest = latestByKey.get(key);
    return {
      answer: latest?.answer ?? "",
      comment: latest?.text ?? "",
      saving: false,
      error: null
    };
  };

  const getDraft = (key: string): ItemDraft => drafts[key] ?? baseDraft(key);

  const updateDraft = (key: string, patch: Partial<ItemDraft>) => {
    setDrafts((prev) => ({
      ...prev,
      [key]: { ...(prev[key] ?? baseDraft(key)), ...patch }
    }));
  };

  const handleSave = async (key: string) => {
    const draft = getDraft(key);
    if (!draft.answer) return;
    updateDraft(key, { saving: true, error: null });
    try {
      const err = await onAnswer(
        key,
        draft.answer,
        draft.comment.trim() ? draft.comment.trim() : null,
        version
      );
      if (typeof err === "string") {
        updateDraft(key, { error: err });
      }
    } catch (e: unknown) {
      updateDraft(key, { error: serverMessage(e) ?? t("investigation.loadError") });
    } finally {
      updateDraft(key, { saving: false });
    }
  };

  return (
    <Grid container spacing={2} sx={{ alignItems: "flex-start" }}>
      <Grid size={{ xs: 12, lg: 7 }}>
        {sortedItems.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("checklist.empty")}
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {sortedItems.map((item) => {
              const latest = latestByKey.get(item.key);
              const draft = getDraft(item.key);

              return (
                <Paper key={item.key} variant="outlined" sx={{ p: 2, borderRadius: 1.5 }}>
                  <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>
                    {item.order}. {item.text}
                  </Typography>

                  {canEdit ? (
                    <Stack spacing={1.25}>
                      <FormControl>
                        <RadioGroup
                          row
                          aria-label={item.text}
                          value={draft.answer}
                          onChange={(e) =>
                            updateDraft(item.key, {
                              answer: e.target.value as OosChecklistAnswer
                            })
                          }
                        >
                          {CHECKLIST_ANSWERS.map((opt) => (
                            <FormControlLabel
                              key={opt}
                              value={opt}
                              control={<Radio size="small" />}
                              label={t(`checklist.answers.${opt}`)}
                            />
                          ))}
                        </RadioGroup>
                      </FormControl>

                      <TextField
                        size="small"
                        fullWidth
                        label={t("checklist.commentLabel")}
                        value={draft.comment}
                        slotProps={{ htmlInput: { maxLength: 1000 } }}
                        onChange={(e) =>
                          updateDraft(item.key, { comment: e.target.value, error: null })
                        }
                      />

                      {draft.error && <Alert severity="error">{draft.error}</Alert>}

                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 1 }}>
                        <Box>
                          {latest && (
                            <Typography variant="caption" color="text.secondary">
                              {t("checklist.recordedBy", {
                                name: latest.recordedByName,
                                date: formatDateTimeWithTimeZone(latest.recordedAtUtc)
                              })}
                            </Typography>
                          )}
                        </Box>
                        <Button
                          size="small"
                          variant="outlined"
                          disabled={!draft.answer || draft.saving}
                          onClick={() => void handleSave(item.key)}
                        >
                          {t("checklist.saveAnswer")}
                        </Button>
                      </Box>
                    </Stack>
                  ) : (
                    <Stack spacing={0.5}>
                      <Typography variant="body2">
                        {latest?.answer
                          ? t(`checklist.answers.${latest.answer}`)
                          : "—"}
                      </Typography>
                      {latest?.text && (
                        <Typography variant="body2" color="text.secondary">
                          {latest.text}
                        </Typography>
                      )}
                      {latest && (
                        <Typography variant="caption" color="text.secondary">
                          {t("checklist.recordedBy", {
                            name: latest.recordedByName,
                            date: formatDateTimeWithTimeZone(latest.recordedAtUtc)
                          })}
                        </Typography>
                      )}
                    </Stack>
                  )}
                </Paper>
              );
            })}
          </Stack>
        )}
      </Grid>

      <Grid size={{ xs: 12, lg: 5 }}>
        <FactsPanel facts={facts} results={results} loading={factsLoading} />
      </Grid>
    </Grid>
  );
}
