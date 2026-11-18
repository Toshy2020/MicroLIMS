import { useState, useEffect, useMemo } from "react";
import {
  Box,
  Typography,
  Button,
  Alert,
  CircularProgress,
  Stack,
  TextField
} from "@mui/material";
import { Trans } from "react-i18next";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { i18n, useTranslation } from "../../../i18n";
import { getErrorMessage } from "../../../utils/errorMessage";
import { StandardizationResultView } from "./StandardizationResultView";
import { ReplicateRowInputCard, ReplicateRowState, isSelectableLot } from "./ReplicateRowInputCard";
import { SolutionPreparationService } from "../services/SolutionPreparationService";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot,
  LotOption,
  SolutionPreparationListItem,
  TitrantStandardizationResponse,
  StandardizeRequest
} from "../types";

interface Props {
  open: boolean;
  preparation: SolutionPreparationResponse;
  onClose: () => void;
  onSuccess: (result: TitrantStandardizationResponse) => void;
}

export function TitrantStandardizationDialog({
  open,
  preparation,
  onClose,
  onSuccess
}: Props) {
  const { t } = useTranslation("solutionPreparation");
  const tEn = i18n.getFixedT("en", "solutionPreparation");
  const recipe = useMemo<RecipeSnapshot | null>(() => {
    if (!preparation.recipeSnapshotJson) return null;
    try {
      return JSON.parse(preparation.recipeSnapshotJson) as RecipeSnapshot;
    } catch {
      return null;
    }
  }, [preparation.recipeSnapshotJson]);

  const mode = recipe?.standardizationMode ?? "PrimaryStandard";
  const isPrimary = mode === "PrimaryStandard";
  const replicateCount = Math.max(1, recipe?.replicateCount ?? 3);
  const blankRequired = Boolean(recipe?.blankRequired);

  const [rows, setRows] = useState<ReplicateRowState[]>([]);
  const [standardLots, setStandardLots] = useState<LotOption[]>([]);
  const [referenceOptions, setReferenceOptions] = useState<SolutionPreparationListItem[]>([]);
  // A failed load is not "no usable lots in inventory".
  const [optionsLoadFailed, setOptionsLoadFailed] = useState(false);
  const [optionsLoading, setOptionsLoading] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [temperatureC, setTemperatureC] = useState("");

  const [signatureOpen, setSignatureOpen] = useState(false);
  const [signatureComment, setSignatureComment] = useState("");
  const [result, setResult] = useState<TitrantStandardizationResponse | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) {
      setResult(null);
      setValidationError(null);
      setSignatureOpen(false);
      setSignatureComment("");
      setSubmitting(false);
      setTemperatureC("");
      return;
    }

    setRows(
      Array.from({ length: replicateCount }, () => ({
        standardMaterialId: null,
        standardWeightMg: "",
        referencePreparationId: null,
        referenceVolumeMl: "",
        titrantVolumeMl: "",
        blankMl: ""
      }))
    );

    setOptionsLoading(true);
    setOptionsLoadFailed(false);
    if (isPrimary) {
      SolutionPreparationService.getStandardLotOptions(preparation.id)
        .then((lots) => {
          setStandardLots(lots);
          const firstUsable = lots.find(isSelectableLot);
          if (firstUsable) {
            setRows((prev) =>
              prev.map((r) => ({ ...r, standardMaterialId: firstUsable.materialId }))
            );
          }
        })
        .catch(() => {
          setStandardLots([]);
          setOptionsLoadFailed(true);
        })
        .finally(() => setOptionsLoading(false));
    } else {
      SolutionPreparationService.getReferenceOptions(preparation.id)
        .then((refs) => {
          setReferenceOptions(refs);
          if (refs.length > 0) {
            setRows((prev) =>
              prev.map((r) => ({ ...r, referencePreparationId: refs[0].id }))
            );
          }
        })
        .catch(() => {
          setReferenceOptions([]);
          setOptionsLoadFailed(true);
        })
        .finally(() => setOptionsLoading(false));
    }
  }, [open, preparation.id, replicateCount, isPrimary]);

  const updateRow = (index: number, patch: Partial<ReplicateRowState>) => {
    setRows((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], ...patch };
      if (patch.standardMaterialId !== undefined) {
        for (let i = index + 1; i < next.length; i++) {
          if (next[i].standardMaterialId === null) {
            next[i].standardMaterialId = patch.standardMaterialId;
          }
        }
      }
      if (patch.referencePreparationId !== undefined) {
        for (let i = index + 1; i < next.length; i++) {
          if (next[i].referencePreparationId === null) {
            next[i].referencePreparationId = patch.referencePreparationId;
          }
        }
      }
      return next;
    });
  };

  const validate = (): string | null => {
    if (temperatureC.trim() !== "" && isNaN(Number(temperatureC))) return t("standardizationDialog.validation.temperatureNumber");
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i];
      const repNo = i + 1;
      if (isPrimary) {
        if (!r.standardMaterialId) return t("standardizationDialog.validation.standardLotRequired", { rep: repNo });
        const w = parseFloat(r.standardWeightMg);
        if (isNaN(w) || w <= 0) return t("standardizationDialog.validation.standardWeightPositive", { rep: repNo });
      } else {
        if (!r.referencePreparationId) return t("standardizationDialog.validation.referenceRequired", { rep: repNo });
        const v = parseFloat(r.referenceVolumeMl);
        if (isNaN(v) || v <= 0) return t("standardizationDialog.validation.referenceVolumePositive", { rep: repNo });
      }

      const titrantVol = parseFloat(r.titrantVolumeMl);
      if (isNaN(titrantVol) || titrantVol <= 0) {
        return t("standardizationDialog.validation.titrantVolumePositive", { rep: repNo });
      }

      if (blankRequired) {
        const b = parseFloat(r.blankMl);
        if (isNaN(b) || b < 0) return t("standardizationDialog.validation.blankNonNegative", { rep: repNo });
        if (titrantVol <= b) return t("standardizationDialog.validation.titrantGreaterThanBlank", { rep: repNo });
      }
    }
    return null;
  };

  const handleProceedToSign = () => {
    if (submitting) return;
    const err = validate();
    if (err) {
      setValidationError(err);
      return;
    }
    setValidationError(null);
    setSignatureOpen(true);
  };

  const handleConfirmSignature = async (password: string) => {
    if (submitting) return;
    setSubmitting(true);
    try {
      const req: StandardizeRequest = {
        replicates: rows.map((r) => ({
          standardMaterialId: isPrimary ? (r.standardMaterialId ?? undefined) : undefined,
          standardWeightMg: isPrimary ? parseFloat(r.standardWeightMg) : undefined,
          referencePreparationId: !isPrimary ? (r.referencePreparationId ?? undefined) : undefined,
          referenceVolumeMl: !isPrimary ? parseFloat(r.referenceVolumeMl) : undefined,
          titrantVolumeMl: parseFloat(r.titrantVolumeMl),
          blankMl: blankRequired ? parseFloat(r.blankMl) : undefined
        })),
        temperatureC: temperatureC.trim() !== "" ? Number(temperatureC) : null,
        password,
        comment: signatureComment.trim() || null
      };

      const data = await SolutionPreparationService.standardize(preparation.id, req);
      setSignatureOpen(false);
      // The result replaces the form - only Close is offered, so it cannot be submitted twice.
      setResult(data);
      onSuccess(data);
    } catch (err: unknown) {
      setValidationError(getErrorMessage(err, t("standardizationDialog.standardizationFailed")));
      throw err;
    } finally {
      setSubmitting(false);
    }
  };

  const dialogTitle = result
    ? t("standardizationDialog.resultTitle")
    : t("standardizationDialog.standardizeTitle", { label: preparation.code || preparation.solutionMasterName });

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={dialogTitle}
        onClose={submitting ? () => {} : onClose}
        maxWidth="md"
        actions={
          result ? (
            <Button variant="contained" onClick={onClose}>
              {t("standardizationDialog.close")}
            </Button>
          ) : (
            <>
              <Button onClick={onClose} disabled={submitting}>{t("standardizationDialog.cancel")}</Button>
              <Button
                variant="contained"
                onClick={handleProceedToSign}
                disabled={
                  submitting ||
                  optionsLoading ||
                  (isPrimary ? standardLots.length === 0 : referenceOptions.length === 0)
                }
              >
                {submitting ? t("standardizationDialog.submitting") : t("standardizationDialog.signAndSubmit")}
              </Button>
            </>
          )
        }
      >
        {result ? (
          <StandardizationResultView
            result={result}
            recipe={recipe}
            blankRequired={blankRequired}
          />
        ) : (
          <Stack spacing={2.5}>
            <Box sx={{ p: 1.5, bgcolor: "action.hover", borderRadius: 1 }}>
              <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                {t("standardizationDialog.parametersTitle", {
                  mode: isPrimary ? t("standardizationDialog.modePrimary") : t("standardizationDialog.modeReference")
                })}
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                <Trans
                  i18nKey="standardizationDialog.parametersSummary"
                  ns="solutionPreparation"
                  values={{
                    min: recipe?.factorMin ?? "—",
                    max: recipe?.factorMax ?? "—",
                    maxRsd: recipe?.maxRsdPercent ?? "—",
                    replicates: replicateCount,
                    blank: blankRequired ? t("standardizationDialog.blankRequired") : t("standardizationDialog.blankNone")
                  }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>

            <TextField
              size="small"
              type="number"
              label={t("standardizationDialog.temperatureLabel")}
              value={temperatureC}
              onChange={(e) => setTemperatureC(e.target.value)}
              helperText={t("standardizationDialog.temperatureHelper")}
              slotProps={{ htmlInput: { step: "any" } }}
              sx={{ maxWidth: 240 }}
            />

            {validationError && <Alert severity="error">{validationError}</Alert>}

            {optionsLoading ? (
              <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}><CircularProgress size={24} /></Box>
            ) : optionsLoadFailed ? (
              <Alert severity="error">
                {t("standardizationDialog.loadFailed", {
                  target: isPrimary ? t("standardizationDialog.targetStandardLots") : t("standardizationDialog.targetReferencePreps")
                })}
              </Alert>
            ) : isPrimary && standardLots.length === 0 ? (
              <Alert severity="warning">{t("standardizationDialog.noStandardLots")}</Alert>
            ) : !isPrimary && referenceOptions.length === 0 ? (
              <Alert severity="warning">{t("standardizationDialog.noReferencePreps")}</Alert>
            ) : (
              rows.map((row, idx) => (
                <ReplicateRowInputCard
                  key={idx}
                  row={row}
                  index={idx}
                  isPrimary={isPrimary}
                  blankRequired={blankRequired}
                  standardLots={standardLots}
                  referenceOptions={referenceOptions}
                  onChange={(patch) => updateRow(idx, patch)}
                />
              ))
            )}
          </Stack>
        )}
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={tEn("standardizationDialog.signatureMeaning")}
        meaningStatementTranslation={t("standardizationDialog.signatureMeaning")}
        showComment
        comment={signatureComment}
        onCommentChange={setSignatureComment}
        onCancel={() => { if (!submitting) setSignatureOpen(false); }}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
