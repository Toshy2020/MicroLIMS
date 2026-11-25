import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Divider,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Paper,
  Radio,
  RadioGroup,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../i18n";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { SignatureDialog } from "../../../components/SignatureDialog";
import type {
  OosCategoryDto,
  OosChecklistAnswer,
  OosChecklistFactsDto,
  OosInvestigationDetailDto,
  OosPhaseOneAssessment,
  SignPhaseOneRequest
} from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { serverMessage } from "../../../utils/errorMessage";
import { ChecklistPanel } from "./ChecklistPanel";

interface PhaseOneSectionProps {
  detail: OosInvestigationDetailDto;
  facts: OosChecklistFactsDto | null;
  factsLoading: boolean;
  categories: OosCategoryDto[];
  canContribute: boolean;
  canManage: boolean;
  onAddStatement: (text: string, version: number) => Promise<string | null | void>;
  onAnswerChecklist: (
    itemKey: string,
    answer: OosChecklistAnswer,
    comment: string | null,
    version: number
  ) => Promise<string | null | void>;
  onSignPhaseOne: (request: SignPhaseOneRequest, version: number) => Promise<void>;
}

export function PhaseOneSection({
  detail,
  facts,
  factsLoading,
  categories,
  canContribute,
  canManage,
  onAddStatement,
  onAnswerChecklist,
  onSignPhaseOne
}: PhaseOneSectionProps) {
  const { t, i18n } = useTranslation("oosTracking");
  const tEn = i18n.getFixedT("en", "oosTracking");
  const isPhaseOne = detail.status === "PhaseOne";
  const statements = detail.entries.filter(
    (e) => e.phase === 1 && e.kind === "AnalystStatement"
  );
  const phaseOneItems = detail.checklistItems.filter((i) => i.phase === 1);

  const [statementText, setStatementText] = useState("");
  const [statementError, setStatementError] = useState<string | null>(null);
  const [submittingStatement, setSubmittingStatement] = useState(false);

  const [assessment, setAssessment] = useState<OosPhaseOneAssessment>(
    "LaboratoryErrorConfirmed"
  );
  const [causeCategoryId, setCauseCategoryId] = useState<number | "">("");
  const [causeDescription, setCauseDescription] = useState("");
  const [signOpen, setSignOpen] = useState(false);

  const assignableCategories = categories.filter(
    (c) => c.isActive && (assessment !== "LaboratoryErrorConfirmed" || !c.isNoCauseFound)
  );
  const selectedCategory = categories.find((c) => c.id === causeCategoryId);
  const errorConfirmedMissingCause =
    assessment === "LaboratoryErrorConfirmed" &&
    (!selectedCategory ||
      selectedCategory.isNoCauseFound ||
      !causeDescription.trim());

  const handleAddStatement = async () => {
    const trimmed = statementText.trim();
    if (!trimmed) return;
    setStatementError(null);
    setSubmittingStatement(true);
    try {
      const err = await onAddStatement(trimmed, detail.version);
      if (typeof err === "string") {
        setStatementError(err);
        return;
      }
      setStatementText("");
    } catch (e: unknown) {
      setStatementError(serverMessage(e) ?? t("investigation.loadError"));
    } finally {
      setSubmittingStatement(false);
    }
  };

  const handleConfirmSign = async (password: string) => {
    await onSignPhaseOne(
      {
        assessment,
        causeCategoryId: causeCategoryId === "" ? null : causeCategoryId,
        causeDescription: causeDescription.trim() || null,
        password
      },
      detail.version
    );
    setSignOpen(false);
  };

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 2 }}>
        <SectionTitle>{t("phaseOne.title")}</SectionTitle>
      </Box>

      <Stack spacing={3}>
        {detail.kind === "Oos" && (
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
              {t("phaseOne.statementsTitle")}
            </Typography>
            <Stack spacing={1} sx={{ mb: 1.5 }}>
              {statements.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  {t("phaseOne.noStatements")}
                </Typography>
              ) : (
                statements.map((st) => (
                  <Paper
                    key={st.id}
                    variant="outlined"
                    sx={{ p: 1.5, bgcolor: "background.default" }}
                  >
                    <Typography variant="body2">{st.text}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {st.recordedByName} · {formatDateTimeWithTimeZone(st.recordedAtUtc)}
                    </Typography>
                  </Paper>
                ))
              )}
            </Stack>

            {isPhaseOne && canContribute && (
              <Stack spacing={1}>
                <TextField
                  size="small"
                  multiline
                  rows={2}
                  label={t("phaseOne.statementLabel")}
                  placeholder={t("phaseOne.statementPlaceholder")}
                  value={statementText}
                  onChange={(e) => {
                    setStatementText(e.target.value);
                    if (statementError) setStatementError(null);
                  }}
                />
                {statementError && (
                  <Alert severity="error">{statementError}</Alert>
                )}
                <Box>
                  <Button
                    variant="outlined"
                    size="small"
                    disabled={!statementText.trim() || submittingStatement}
                    onClick={() => void handleAddStatement()}
                  >
                    {t("phaseOne.addStatement")}
                  </Button>
                </Box>
              </Stack>
            )}
          </Box>
        )}

        <Divider />

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
            {t("phaseOne.checklistTitle")}
          </Typography>
          <ChecklistPanel
            items={phaseOneItems}
            entries={detail.entries.filter((e) => e.phase === 1)}
            facts={facts}
            results={detail.results}
            factsLoading={factsLoading}
            canEdit={isPhaseOne && canContribute}
            version={detail.version}
            onAnswer={(itemKey, answer, comment, version) =>
              onAnswerChecklist(itemKey, answer, comment, version)
            }
          />
        </Box>

        {detail.kind === "Oos" && (
          <>
            <Divider />
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                {t("phaseOne.assessmentTitle")}
              </Typography>

              {detail.phaseOneSignedAtUtc && detail.phaseOneAssessment ? (
                <Stack spacing={1}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <StatusBadge status={detail.phaseOneAssessment} />
                    {detail.phaseOneCauseCategoryName && (
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {detail.phaseOneCauseCategoryName}
                      </Typography>
                    )}
                  </Box>
                  {detail.phaseOneCauseDescription && (
                    <Typography variant="body2">
                      {detail.phaseOneCauseDescription}
                    </Typography>
                  )}
                  <Typography variant="caption" color="text.secondary">
                    {t("phaseOne.signedBy", {
                      name: detail.phaseOneSignedByName ?? "",
                      date: formatDateTimeWithTimeZone(detail.phaseOneSignedAtUtc)
                    })}
                  </Typography>
                </Stack>
              ) : isPhaseOne && canManage ? (
                <Stack spacing={2}>
                  <FormControl>
                    <RadioGroup
                      row
                      aria-label={t("phaseOne.assessmentLabel")}
                      value={assessment}
                      onChange={(e) => {
                        const next = e.target.value as OosPhaseOneAssessment;
                        setAssessment(next);
                        if (
                          next === "LaboratoryErrorConfirmed" &&
                          selectedCategory?.isNoCauseFound
                        ) {
                          setCauseCategoryId("");
                        }
                      }}
                    >
                      <FormControlLabel
                        value="LaboratoryErrorConfirmed"
                        control={<Radio size="small" />}
                        label={t("statuses.LaboratoryErrorConfirmed", { ns: "common" })}
                      />
                      <FormControlLabel
                        value="NoLaboratoryErrorFound"
                        control={<Radio size="small" />}
                        label={t("statuses.NoLaboratoryErrorFound", { ns: "common" })}
                      />
                    </RadioGroup>
                  </FormControl>

                  {assessment === "LaboratoryErrorConfirmed" && (
                    <Stack spacing={1.5}>
                      <FormControl size="small" fullWidth>
                        <InputLabel id="phase-one-cause-label">
                          {t("phaseOne.causeCategoryLabel")}
                        </InputLabel>
                        <Select
                          labelId="phase-one-cause-label"
                          label={t("phaseOne.causeCategoryLabel")}
                          value={causeCategoryId}
                          onChange={(e) =>
                            setCauseCategoryId(
                              (e.target.value as number | "") === "" ? "" : Number(e.target.value)
                            )
                          }
                        >
                          {assignableCategories.map((cat) => (
                            <MenuItem key={cat.id} value={cat.id}>
                              {cat.name}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>

                      <TextField
                        size="small"
                        multiline
                        rows={2}
                        label={t("phaseOne.causeDescriptionLabel")}
                        value={causeDescription}
                        onChange={(e) => setCauseDescription(e.target.value)}
                      />
                    </Stack>
                  )}

                  {errorConfirmedMissingCause && (
                    <Alert severity="info">
                      {t("phaseOne.errorConfirmedRequiresCause")}
                    </Alert>
                  )}

                  <Box>
                    <Button
                      variant="contained"
                      disabled={errorConfirmedMissingCause}
                      onClick={() => setSignOpen(true)}
                    >
                      {t("phaseOne.signButton")}
                    </Button>
                  </Box>
                </Stack>
              ) : (
                <Typography variant="body2" color="text.secondary">
                  —
                </Typography>
              )}
            </Box>
          </>
        )}
      </Stack>

      <SignatureDialog
        open={signOpen}
        meaningStatement={tEn("phaseOne.meaningStatement")}
        meaningStatementTranslation={t("phaseOne.meaningStatement")}
        onCancel={() => setSignOpen(false)}
        onConfirm={handleConfirmSign}
      />
    </Paper>
  );
}
