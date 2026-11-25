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
  CloseExcursionRequest,
  ConcludeRequest,
  OosCategoryDto,
  OosConclusion,
  OosInvestigationDetailDto
} from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

interface ConclusionSectionProps {
  detail: OosInvestigationDetailDto;
  categories: OosCategoryDto[];
  canManage: boolean;
  onConclude: (request: ConcludeRequest, version: number) => Promise<void>;
  onReopen: (reason: string, password: string, version: number) => Promise<void>;
  onCloseExcursion: (request: CloseExcursionRequest, version: number) => Promise<void>;
}

export function ConclusionSection({
  detail,
  categories,
  canManage,
  onConclude,
  onReopen,
  onCloseExcursion
}: ConclusionSectionProps) {
  const { t, i18n } = useTranslation("oosTracking");
  const tEn = i18n.getFixedT("en", "oosTracking");
  const isExcursion = detail.kind === "Excursion";
  const awaitingRetest = detail.status === "AwaitingRetest";

  const [conclusion, setConclusion] = useState<OosConclusion>(
    awaitingRetest ? "Invalidated" : "Confirmed"
  );
  const [causeCategoryId, setCauseCategoryId] = useState<number | "">("");
  const [causeDescription, setCauseDescription] = useState("");
  const [summary, setSummary] = useState("");
  const [actionTaken, setActionTaken] = useState("");
  const [signMode, setSignMode] = useState<"conclude" | "excursion" | "reopen" | null>(
    null
  );
  const [reopenReason, setReopenReason] = useState("");

  const activeCategories = categories.filter((c) => c.isActive);
  const selectedCategory = activeCategories.find((c) => c.id === causeCategoryId);
  const noCauseFoundSelected = Boolean(selectedCategory?.isNoCauseFound);

  const effectiveConclusion: OosConclusion = awaitingRetest
    ? "Invalidated"
    : noCauseFoundSelected && conclusion === "Invalidated"
      ? "Confirmed"
      : conclusion;

  const canSubmitConclude =
    Boolean(summary.trim()) &&
    (awaitingRetest ||
      (effectiveConclusion === "Confirmed"
        ? causeCategoryId !== ""
        : causeCategoryId !== "" && !noCauseFoundSelected && Boolean(causeDescription.trim())));

  const canSubmitExcursion =
    causeCategoryId !== "" &&
    Boolean(causeDescription.trim()) &&
    Boolean(actionTaken.trim());

  const handleConfirm = async (password: string) => {
    if (signMode === "conclude") {
      await onConclude(
        {
          conclusion: effectiveConclusion,
          rootCauseCategoryId:
            awaitingRetest
              ? detail.phaseOneCauseCategoryId
              : causeCategoryId === ""
                ? null
                : causeCategoryId,
          rootCauseDescription: awaitingRetest
            ? detail.phaseOneCauseDescription
            : causeDescription.trim() || null,
          summary: summary.trim(),
          password
        },
        detail.version
      );
    } else if (signMode === "excursion" && causeCategoryId !== "") {
      await onCloseExcursion(
        {
          causeCategoryId,
          causeDescription: causeDescription.trim(),
          actionTaken: actionTaken.trim(),
          password
        },
        detail.version
      );
    } else if (signMode === "reopen") {
      await onReopen(reopenReason.trim(), password, detail.version);
      setReopenReason("");
    }
    setSignMode(null);
  };

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 2 }}>
        <SectionTitle>
          {isExcursion ? t("conclusion.excursionTitle") : t("conclusion.title")}
        </SectionTitle>
      </Box>

      <Stack spacing={2}>
        {detail.status === "Concluded" && detail.conclusion ? (
          <Stack spacing={1.5}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <StatusBadge status={detail.conclusion} />
              {detail.rootCauseCategoryName && (
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {detail.rootCauseCategoryName}
                </Typography>
              )}
            </Box>
            {detail.rootCauseDescription && (
              <Typography variant="body2">{detail.rootCauseDescription}</Typography>
            )}
            {detail.conclusionSummary && (
              <Typography variant="body2">{detail.conclusionSummary}</Typography>
            )}
            {detail.concludedAtUtc && (
              <Typography variant="caption" color="text.secondary">
                {detail.concludedByName} · {formatDateTimeWithTimeZone(detail.concludedAtUtc)}
              </Typography>
            )}

            {canManage && !isExcursion && (
              <Box sx={{ pt: 1 }}>
                <Stack spacing={1}>
                  <TextField
                    size="small"
                    label={t("conclusion.reopenReasonLabel")}
                    value={reopenReason}
                    onChange={(e) => setReopenReason(e.target.value)}
                  />
                  <Box>
                    <Button
                      variant="outlined"
                      color="warning"
                      disabled={!reopenReason.trim()}
                      onClick={() => setSignMode("reopen")}
                    >
                      {t("conclusion.reopenButton")}
                    </Button>
                  </Box>
                </Stack>
              </Box>
            )}
          </Stack>
        ) : isExcursion && canManage ? (
          <Stack spacing={2}>
            <FormControl size="small" fullWidth>
              <InputLabel id="excursion-cause-label">
                {t("conclusion.causeCategoryLabel")}
              </InputLabel>
              <Select
                labelId="excursion-cause-label"
                label={t("conclusion.causeCategoryLabel")}
                value={causeCategoryId}
                onChange={(e) =>
                  setCauseCategoryId(
                    (e.target.value as number | "") === "" ? "" : Number(e.target.value)
                  )
                }
              >
                {activeCategories.map((cat) => (
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
              label={t("conclusion.causeDescriptionLabel")}
              value={causeDescription}
              onChange={(e) => setCauseDescription(e.target.value)}
            />
            <TextField
              size="small"
              multiline
              rows={2}
              label={t("conclusion.actionTakenLabel")}
              value={actionTaken}
              onChange={(e) => setActionTaken(e.target.value)}
            />
            <Box>
              <Button
                variant="contained"
                disabled={!canSubmitExcursion}
                onClick={() => setSignMode("excursion")}
              >
                {t("conclusion.signCloseExcursion")}
              </Button>
            </Box>
          </Stack>
        ) : !isExcursion &&
          canManage &&
          (detail.status === "AwaitingRetest" || detail.status === "PhaseTwo") ? (
          <Stack spacing={2}>
            {!awaitingRetest && (
              <>
                <FormControl size="small" fullWidth>
                  <InputLabel id="conclusion-cause-label">
                    {t("conclusion.causeCategoryLabel")}
                  </InputLabel>
                  <Select
                    labelId="conclusion-cause-label"
                    label={t("conclusion.causeCategoryLabel")}
                    value={causeCategoryId}
                    onChange={(e) => {
                      const raw = e.target.value as number | "";
                      const nextId = raw === "" ? "" : Number(raw);
                      setCauseCategoryId(nextId);
                      const cat = activeCategories.find((c) => c.id === nextId);
                      if (cat?.isNoCauseFound) {
                        setConclusion("Confirmed");
                      }
                    }}
                  >
                    {activeCategories.map((cat) => (
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
                  label={t("conclusion.causeDescriptionLabel")}
                  value={causeDescription}
                  onChange={(e) => setCauseDescription(e.target.value)}
                />
              </>
            )}

            <FormControl>
              <RadioGroup
                row
                aria-label={t("conclusion.decisionLabel")}
                value={effectiveConclusion}
                onChange={(e) => setConclusion(e.target.value as OosConclusion)}
              >
                <FormControlLabel
                  value="Confirmed"
                  disabled={awaitingRetest}
                  control={<Radio size="small" />}
                  label={t("conclusion.confirmed")}
                />
                <FormControlLabel
                  value="Invalidated"
                  disabled={noCauseFoundSelected}
                  control={<Radio size="small" />}
                  label={t("conclusion.invalidated")}
                />
              </RadioGroup>
            </FormControl>

            {noCauseFoundSelected && (
              <Alert severity="info">
                {t("conclusion.noCauseFoundBlocksInvalidated")}
              </Alert>
            )}

            <TextField
              size="small"
              multiline
              rows={2}
              label={t("conclusion.summaryLabel")}
              value={summary}
              onChange={(e) => setSummary(e.target.value)}
            />

            <Box>
              <Button
                variant="contained"
                disabled={!canSubmitConclude}
                onClick={() => setSignMode("conclude")}
              >
                {t("conclusion.signConclude")}
              </Button>
            </Box>
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary">
            —
          </Typography>
        )}

        {detail.reopenings.length > 0 && (
          <>
            <Divider />
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                {t("conclusion.reopeningsTitle")}
              </Typography>
              <Stack spacing={1}>
                {detail.reopenings.map((r) => (
                  <Paper key={r.id} variant="outlined" sx={{ p: 1.5, bgcolor: "background.default" }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t("conclusion.previousConclusion", {
                        conclusion: t(`statuses.${r.previousConclusion}`, { ns: "common" })
                      })}
                    </Typography>
                    <Typography variant="body2">{r.reason}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {t("conclusion.reopenedBy", {
                        name: r.reopenedByName,
                        date: formatDateTimeWithTimeZone(r.reopenedAtUtc)
                      })}
                    </Typography>
                  </Paper>
                ))}
              </Stack>
            </Box>
          </>
        )}
      </Stack>

      <SignatureDialog
        open={signMode !== null}
        meaningStatement={
          signMode === "excursion"
            ? tEn("conclusion.excursionMeaning")
            : signMode === "reopen"
              ? tEn("conclusion.reopenMeaning")
              : tEn("conclusion.concludedMeaning")
        }
        meaningStatementTranslation={
          signMode === "excursion"
            ? t("conclusion.excursionMeaning")
            : signMode === "reopen"
              ? t("conclusion.reopenMeaning")
              : t("conclusion.concludedMeaning")
        }
        onCancel={() => setSignMode(null)}
        onConfirm={handleConfirm}
      />
    </Paper>
  );
}
