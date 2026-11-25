import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControl,
  FormControlLabel,
  FormGroup,
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
import { monospaceFontFamily } from "../../../theme/palette";
import type {
  OosInvestigationDetailDto,
  OosRetestPlanDetailDto,
  OosRetestSource,
  SaveRetestPlanRequest
} from "../../../services/oosInvestigationService";
import type { UserRecord } from "../../users/services/UserService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { serverMessage } from "../../../utils/errorMessage";

interface RetestPlanSectionProps {
  detail: OosInvestigationDetailDto;
  analysts: UserRecord[];
  canManage: boolean;
  onSaveDraft: (request: SaveRetestPlanRequest, version: number) => Promise<string | null | void>;
  onSignPlan: (planId: number, password: string, version: number) => Promise<void>;
}

interface RetestPlanDraftFormProps {
  detail: OosInvestigationDetailDto;
  analysts: UserRecord[];
  unsignedPlan: OosRetestPlanDetailDto | null;
  nextRevision: number;
  needsAmendmentReason: boolean;
  onSaveDraft: (request: SaveRetestPlanRequest, version: number) => Promise<string | null | void>;
}

function RetestPlanDraftForm({
  detail,
  analysts,
  unsignedPlan,
  nextRevision,
  needsAmendmentReason,
  onSaveDraft
}: RetestPlanDraftFormProps) {
  const { t } = useTranslation("oosTracking");

  const [source, setSource] = useState<OosRetestSource>(
    unsignedPlan?.source ?? "RetainedSample"
  );
  const [retestCount, setRetestCount] = useState<number>(
    unsignedPlan?.retestCount ?? 1
  );
  const [testOrderIds, setTestOrderIds] = useState<number[]>(
    unsignedPlan?.testOrderIds ?? detail.results.map((r) => r.testOrderId)
  );
  const [analystUserIds, setAnalystUserIds] = useState<number[]>(
    unsignedPlan?.analystUserIds ?? []
  );
  const [judgingRule, setJudgingRule] = useState<string>(
    unsignedPlan?.judgingRule ?? ""
  );
  const [amendmentReason, setAmendmentReason] = useState<string>(
    unsignedPlan?.amendmentReason ?? ""
  );
  const [saving, setSaving] = useState(false);
  const [draftError, setDraftError] = useState<string | null>(null);

  const toggleOrder = (orderId: number) => {
    setTestOrderIds((prev) =>
      prev.includes(orderId) ? prev.filter((id) => id !== orderId) : [...prev, orderId]
    );
  };

  const missingAmendmentReason = needsAmendmentReason && !amendmentReason.trim();
  const draftValid =
    retestCount >= 1 &&
    retestCount <= 10 &&
    testOrderIds.length > 0 &&
    analystUserIds.length > 0 &&
    Boolean(judgingRule.trim()) &&
    !missingAmendmentReason;

  const handleSaveDraft = async () => {
    if (!draftValid) return;
    setDraftError(null);
    setSaving(true);
    try {
      const err = await onSaveDraft(
        {
          source,
          retestCount,
          testOrderIds,
          analystUserIds,
          judgingRule: judgingRule.trim(),
          amendmentReason: amendmentReason.trim() || null
        },
        detail.version
      );
      if (typeof err === "string") {
        setDraftError(err);
      }
    } catch (e: unknown) {
      setDraftError(serverMessage(e) ?? t("investigation.loadError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 1 }}>
      <Stack spacing={2}>
        <FormControl>
          <RadioGroup
            row
            aria-label={t("retestPlan.source")}
            value={source}
            onChange={(e) => setSource(e.target.value as OosRetestSource)}
          >
            <FormControlLabel
              value="RetainedSample"
              control={<Radio size="small" />}
              label={t("retestPlan.sources.RetainedSample")}
            />
            <FormControlLabel
              value="NewSample"
              control={<Radio size="small" />}
              label={t("retestPlan.sources.NewSample")}
            />
          </RadioGroup>
        </FormControl>

        <TextField
          size="small"
          type="number"
          label={t("retestPlan.retestCount")}
          value={retestCount}
          slotProps={{ htmlInput: { min: 1, max: 10 } }}
          onChange={(e) => setRetestCount(Number(e.target.value))}
          sx={{ maxWidth: 240 }}
        />

        <Box>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
            {t("retestPlan.tests")}
          </Typography>
          <FormGroup row>
            {detail.results.map((r) => (
              <FormControlLabel
                key={r.testOrderId}
                control={
                  <Checkbox
                    size="small"
                    checked={testOrderIds.includes(r.testOrderId)}
                    onChange={() => toggleOrder(r.testOrderId)}
                  />
                }
                label={`${r.testCode} (${r.testDisplayName})`}
              />
            ))}
          </FormGroup>
        </Box>

        <FormControl size="small" fullWidth>
          <InputLabel id="retest-analysts-label">
            {t("retestPlan.analysts")}
          </InputLabel>
          <Select
            labelId="retest-analysts-label"
            label={t("retestPlan.analysts")}
            multiple
            value={analystUserIds}
            onChange={(e) => {
              const val = e.target.value;
              setAnalystUserIds(
                typeof val === "string"
                  ? val.split(",").map(Number)
                  : val.map(Number)
              );
            }}
          >
            {analysts.map((a) => (
              <MenuItem key={a.id} value={a.id}>
                {a.fullName} ({a.username})
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          multiline
          rows={2}
          label={t("retestPlan.judgingRule")}
          value={judgingRule}
          onChange={(e) => setJudgingRule(e.target.value)}
        />

        {needsAmendmentReason && (
          <>
            <TextField
              size="small"
              label={t("retestPlan.amendmentReason")}
              value={amendmentReason}
              onChange={(e) => setAmendmentReason(e.target.value)}
              required
            />
            {missingAmendmentReason && (
              <Alert severity="info">
                {t("retestPlan.amendmentRequired", { rev: nextRevision })}
              </Alert>
            )}
          </>
        )}

        {draftError && <Alert severity="error">{draftError}</Alert>}

        <Box>
          <Button
            variant="outlined"
            disabled={!draftValid || saving}
            onClick={() => void handleSaveDraft()}
          >
            {t("retestPlan.saveDraft")}
          </Button>
          {!draftValid && !missingAmendmentReason && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.75 }}>
              {t("retestPlan.draftMissingHint")}
            </Typography>
          )}
        </Box>
      </Stack>
    </Paper>
  );
}

export function RetestPlanSection({
  detail,
  analysts,
  canManage,
  onSaveDraft,
  onSignPlan
}: RetestPlanSectionProps) {
  const { t, i18n } = useTranslation("oosTracking");
  const tEn = i18n.getFixedT("en", "oosTracking");
  const canPlan =
    canManage &&
    (detail.status === "AwaitingRetest" || detail.status === "PhaseTwo");

  const unsignedPlan = detail.retestPlans.find((p) => !p.signedAtUtc) ?? null;
  const hasSignedPlan = detail.retestPlans.some((p) => Boolean(p.signedAtUtc));
  const nextRevision =
    unsignedPlan?.revision ??
    (detail.retestPlans.length > 0
      ? Math.max(...detail.retestPlans.map((p) => p.revision)) + 1
      : 1);
  const needsAmendmentReason = nextRevision > 1 || hasSignedPlan;

  const [signPlanId, setSignPlanId] = useState<number | null>(null);

  const unsignedPlanKey = unsignedPlan
    ? `${unsignedPlan.id}:${unsignedPlan.revision}:${unsignedPlan.source}:${unsignedPlan.retestCount}:${unsignedPlan.judgingRule}:${unsignedPlan.amendmentReason ?? ""}:${unsignedPlan.testOrderIds.join(",")}:${unsignedPlan.analystUserIds.join(",")}`
    : `none:${detail.results.map((r) => r.testOrderId).join(",")}`;

  const handleSignConfirm = async (password: string) => {
    if (signPlanId === null) return;
    await onSignPlan(signPlanId, password, detail.version);
    setSignPlanId(null);
  };

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 2 }}>
        <SectionTitle>{t("retestPlan.title")}</SectionTitle>
      </Box>

      <Stack spacing={2}>
        {detail.retestPlans.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("retestPlan.empty")}
          </Typography>
        ) : (
          detail.retestPlans.map((plan) => (
            <Paper
              key={plan.id}
              variant="outlined"
              sx={{ p: 2, bgcolor: "background.default", borderRadius: 1 }}
            >
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  flexWrap: "wrap",
                  gap: 1,
                  mb: 1
                }}
              >
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {t("retestPlan.revision", { rev: plan.revision })}
                  </Typography>
                  <Chip
                    size="small"
                    label={
                      plan.signedAtUtc
                        ? t("retestPlan.signedBadge")
                        : t("retestPlan.draftBadge")
                    }
                    color={plan.signedAtUtc ? "success" : "default"}
                  />
                </Stack>
                {canPlan && !plan.signedAtUtc && (
                  <Button
                    variant="contained"
                    size="small"
                    onClick={() => setSignPlanId(plan.id)}
                  >
                    {t("retestPlan.signAndCreate")}
                  </Button>
                )}
              </Box>

              <Typography variant="body2">
                {t("retestPlan.summaryLine", {
                  source: t(`retestPlan.sources.${plan.source}`),
                  count: plan.retestCount,
                  rule: plan.judgingRule
                })}
              </Typography>
              {plan.amendmentReason && (
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {t("retestPlan.amendmentReason")}: {plan.amendmentReason}
                </Typography>
              )}
              {plan.signedAtUtc && (
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {plan.signedByName} · {formatDateTimeWithTimeZone(plan.signedAtUtc)}
                </Typography>
              )}

              {plan.samples.length > 0 ? (
                <Box sx={{ mt: 1.5 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                    {t("retestPlan.linkedSamples")}
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
                    {plan.samples.map((s) => (
                      <Chip
                        key={s.sampleId}
                        size="small"
                        variant="outlined"
                        label={
                          <Box component="span" sx={{ display: "inline-flex", alignItems: "center", gap: 0.75 }}>
                            <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>
                              {s.referenceNumber}
                            </Box>
                            <StatusBadge status={s.status} />
                          </Box>
                        }
                      />
                    ))}
                  </Stack>
                </Box>
              ) : (
                !plan.signedAtUtc && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
                    {t("retestPlan.noSamplesYet")}
                  </Typography>
                )
              )}
            </Paper>
          ))
        )}

        {canPlan && (
          <RetestPlanDraftForm
            key={unsignedPlanKey}
            detail={detail}
            analysts={analysts}
            unsignedPlan={unsignedPlan}
            nextRevision={nextRevision}
            needsAmendmentReason={needsAmendmentReason}
            onSaveDraft={onSaveDraft}
          />
        )}
      </Stack>

      <SignatureDialog
        open={signPlanId !== null}
        meaningStatement={tEn("retestPlan.meaningStatement")}
        meaningStatementTranslation={t("retestPlan.meaningStatement")}
        onCancel={() => setSignPlanId(null)}
        onConfirm={handleSignConfirm}
      />
    </Paper>
  );
}
