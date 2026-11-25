import { useState } from "react";
import { Box, Button, Divider, Paper, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { SectionTitle } from "../../../components/SectionTitle";
import type {
  OosChecklistAnswer,
  OosChecklistFactsDto,
  OosEntryKind,
  OosInvestigationDetailDto
} from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { ChecklistPanel } from "./ChecklistPanel";

interface PhaseTwoSectionProps {
  detail: OosInvestigationDetailDto;
  facts: OosChecklistFactsDto | null;
  factsLoading: boolean;
  canContributeOrManage: boolean;
  onAddEntry: (
    kind: OosEntryKind,
    text: string,
    parentEntryId: number | null,
    version: number
  ) => Promise<void>;
  onAnswerChecklist: (
    itemKey: string,
    answer: OosChecklistAnswer,
    comment: string | null,
    version: number
  ) => Promise<void>;
}

export function PhaseTwoSection({
  detail,
  facts,
  factsLoading,
  canContributeOrManage,
  onAddEntry,
  onAnswerChecklist
}: PhaseTwoSectionProps) {
  const { t } = useTranslation("oosTracking");
  const canEdit = canContributeOrManage && detail.status === "PhaseTwo";

  const outsideReviews = detail.entries.filter(
    (e) => e.phase === 2 && e.kind === "OutsideReview"
  );
  const hypotheses = detail.entries.filter(
    (e) => e.phase === 2 && e.kind === "Hypothesis"
  );
  const outcomes = detail.entries.filter(
    (e) => e.phase === 2 && e.kind === "HypothesisOutcome"
  );
  const notes = detail.entries.filter((e) => e.kind === "Note");
  const phaseTwoItems = detail.checklistItems.filter((i) => i.phase === 2);

  const [outsideText, setOutsideText] = useState("");
  const [hypothesisText, setHypothesisText] = useState("");
  const [outcomeTexts, setOutcomeTexts] = useState<Record<number, string>>({});
  const [noteText, setNoteText] = useState("");
  const [busy, setBusy] = useState(false);

  const submitEntry = async (
    kind: OosEntryKind,
    text: string,
    parentEntryId: number | null,
    clear: () => void
  ) => {
    const trimmed = text.trim();
    if (!trimmed) return;
    setBusy(true);
    try {
      await onAddEntry(kind, trimmed, parentEntryId, detail.version);
      clear();
    } finally {
      setBusy(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 2 }}>
        <SectionTitle>{t("phaseTwo.title")}</SectionTitle>
      </Box>

      <Stack spacing={3}>
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            {t("phaseTwo.outsideReviewTitle")}
          </Typography>
          <Stack spacing={1} sx={{ mb: 1.5 }}>
            {outsideReviews.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                {t("phaseTwo.noOutsideReview")}
              </Typography>
            ) : (
              outsideReviews.map((entry) => (
                <Paper key={entry.id} variant="outlined" sx={{ p: 1.5, bgcolor: "background.default" }}>
                  <Typography variant="body2">{entry.text}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {entry.recordedByName} · {formatDateTimeWithTimeZone(entry.recordedAtUtc)}
                  </Typography>
                </Paper>
              ))
            )}
          </Stack>
          {canEdit && (
            <Stack spacing={1}>
              <TextField
                size="small"
                multiline
                rows={2}
                label={t("phaseTwo.outsideReviewLabel")}
                value={outsideText}
                onChange={(e) => setOutsideText(e.target.value)}
              />
              <Box>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={!outsideText.trim() || busy}
                  onClick={() =>
                    void submitEntry("OutsideReview", outsideText, null, () =>
                      setOutsideText("")
                    )
                  }
                >
                  {t("phaseTwo.addOutsideReview")}
                </Button>
              </Box>
            </Stack>
          )}
        </Box>

        <Divider />

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            {t("phaseTwo.hypothesesTitle")}
          </Typography>
          <Stack spacing={1.5} sx={{ mb: 1.5 }}>
            {hypotheses.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                {t("phaseTwo.noHypotheses")}
              </Typography>
            ) : (
              hypotheses.map((hyp) => {
                const hypOutcomes = outcomes.filter((o) => o.parentEntryId === hyp.id);
                return (
                  <Paper key={hyp.id} variant="outlined" sx={{ p: 1.5, bgcolor: "background.default" }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {hyp.text}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {hyp.recordedByName} · {formatDateTimeWithTimeZone(hyp.recordedAtUtc)}
                    </Typography>

                    {hypOutcomes.length > 0 && (
                      <Stack spacing={0.75} sx={{ mt: 1, pl: 2, borderLeft: 2, borderColor: "divider" }}>
                        {hypOutcomes.map((out) => (
                          <Box key={out.id}>
                            <Typography variant="body2">{out.text}</Typography>
                            <Typography variant="caption" color="text.secondary">
                              {out.recordedByName} · {formatDateTimeWithTimeZone(out.recordedAtUtc)}
                            </Typography>
                          </Box>
                        ))}
                      </Stack>
                    )}

                    {canEdit && (
                      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mt: 1.25 }}>
                        <TextField
                          size="small"
                          fullWidth
                          label={t("phaseTwo.outcomeLabel")}
                          value={outcomeTexts[hyp.id] ?? ""}
                          onChange={(e) =>
                            setOutcomeTexts((prev) => ({
                              ...prev,
                              [hyp.id]: e.target.value
                            }))
                          }
                        />
                        <Button
                          size="small"
                          variant="outlined"
                          disabled={!(outcomeTexts[hyp.id] ?? "").trim() || busy}
                          onClick={() =>
                            void submitEntry(
                              "HypothesisOutcome",
                              outcomeTexts[hyp.id] ?? "",
                              hyp.id,
                              () =>
                                setOutcomeTexts((prev) => ({ ...prev, [hyp.id]: "" }))
                            )
                          }
                          sx={{ whiteSpace: "nowrap" }}
                        >
                          {t("phaseTwo.addOutcome")}
                        </Button>
                      </Stack>
                    )}
                  </Paper>
                );
              })
            )}
          </Stack>

          {canEdit && (
            <Stack spacing={1}>
              <TextField
                size="small"
                multiline
                rows={2}
                label={t("phaseTwo.hypothesisLabel")}
                value={hypothesisText}
                onChange={(e) => setHypothesisText(e.target.value)}
              />
              <Box>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={!hypothesisText.trim() || busy}
                  onClick={() =>
                    void submitEntry("Hypothesis", hypothesisText, null, () =>
                      setHypothesisText("")
                    )
                  }
                >
                  {t("phaseTwo.addHypothesis")}
                </Button>
              </Box>
            </Stack>
          )}
        </Box>

        <Divider />

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
            {t("phaseTwo.checklistTitle")}
          </Typography>
          <ChecklistPanel
            items={phaseTwoItems}
            entries={detail.entries.filter((e) => e.phase === 2)}
            facts={facts}
            results={detail.results}
            factsLoading={factsLoading}
            canEdit={canEdit}
            version={detail.version}
            onAnswer={(itemKey, answer, comment, version) =>
              onAnswerChecklist(itemKey, answer, comment, version)
            }
          />
        </Box>

        <Divider />

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            {t("phaseTwo.notesTitle")}
          </Typography>
          <Stack spacing={1} sx={{ mb: 1.5 }}>
            {notes.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                {t("phaseTwo.noNotes")}
              </Typography>
            ) : (
              notes.map((n) => (
                <Paper key={n.id} variant="outlined" sx={{ p: 1.5, bgcolor: "background.default" }}>
                  <Typography variant="body2">{n.text}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {n.recordedByName} · {formatDateTimeWithTimeZone(n.recordedAtUtc)}
                  </Typography>
                </Paper>
              ))
            )}
          </Stack>
          {canEdit && (
            <Stack spacing={1}>
              <TextField
                size="small"
                label={t("phaseTwo.noteLabel")}
                value={noteText}
                onChange={(e) => setNoteText(e.target.value)}
              />
              <Box>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={!noteText.trim() || busy}
                  onClick={() =>
                    void submitEntry("Note", noteText, null, () => setNoteText(""))
                  }
                >
                  {t("phaseTwo.addNote")}
                </Button>
              </Box>
            </Stack>
          )}
        </Box>
      </Stack>
    </Paper>
  );
}
