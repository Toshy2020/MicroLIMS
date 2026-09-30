import { useState, useEffect, useMemo } from "react";
import {
  Box,
  Typography,
  Button,
  Alert,
  CircularProgress,
  Stack
} from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { StandardizationResultView } from "./StandardizationResultView";
import { ReplicateRowInputCard, ReplicateRowState } from "./ReplicateRowInputCard";
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
  const [optionsLoading, setOptionsLoading] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

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
    if (isPrimary) {
      SolutionPreparationService.getStandardLotOptions(preparation.id)
        .then((lots) => {
          setStandardLots(lots);
          const firstUsable = lots.find((l) => l.usable);
          if (firstUsable) {
            setRows((prev) =>
              prev.map((r) => ({ ...r, standardMaterialId: firstUsable.materialId }))
            );
          }
        })
        .catch(() => setStandardLots([]))
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
        .catch(() => setReferenceOptions([]))
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
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i];
      const repNo = i + 1;
      if (isPrimary) {
        if (!r.standardMaterialId) return `Replicate ${repNo}: Standard lot is required.`;
        const w = parseFloat(r.standardWeightMg);
        if (isNaN(w) || w <= 0) return `Replicate ${repNo}: Standard weight must be greater than zero.`;
      } else {
        if (!r.referencePreparationId) return `Replicate ${repNo}: Reference preparation is required.`;
        const v = parseFloat(r.referenceVolumeMl);
        if (isNaN(v) || v <= 0) return `Replicate ${repNo}: Reference volume must be greater than zero.`;
      }

      const titrantVol = parseFloat(r.titrantVolumeMl);
      if (isNaN(titrantVol) || titrantVol <= 0) {
        return `Replicate ${repNo}: Titrant volume must be greater than zero.`;
      }

      if (blankRequired) {
        const b = parseFloat(r.blankMl);
        if (isNaN(b) || b < 0) return `Replicate ${repNo}: Blank volume cannot be negative.`;
        if (titrantVol <= b) return `Replicate ${repNo}: Titrant volume must be greater than blank volume.`;
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
        password,
        comment: signatureComment.trim() || null
      };

      const data = await SolutionPreparationService.standardize(preparation.id, req);
      setSignatureOpen(false);
      // The result replaces the form - only Close is offered, so it cannot be submitted twice.
      setResult(data);
      onSuccess(data);
    } finally {
      setSubmitting(false);
    }
  };

  const dialogTitle = result
    ? "Standardization Result"
    : `Standardize Titrant: ${preparation.code || preparation.solutionMasterName}`;

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
              Close
            </Button>
          ) : (
            <>
              <Button onClick={onClose} disabled={submitting}>Cancel</Button>
              <Button
                variant="contained"
                onClick={handleProceedToSign}
                disabled={
                  submitting ||
                  optionsLoading ||
                  (isPrimary ? standardLots.length === 0 : referenceOptions.length === 0)
                }
              >
                {submitting ? "Submitting..." : "Sign & Submit"}
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
                Standardization Parameters ({isPrimary ? "Primary Standard" : "Against Volumetric Solution"})
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Target Factor: {recipe?.factorMin ?? "—"} – {recipe?.factorMax ?? "—"} · Max RSD: {recipe?.maxRsdPercent ?? "—"}% · Replicates: {replicateCount} · Blank: {blankRequired ? "Required" : "None"}
              </Typography>
            </Box>

            {validationError && <Alert severity="error">{validationError}</Alert>}

            {optionsLoading ? (
              <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}><CircularProgress size={24} /></Box>
            ) : isPrimary && standardLots.length === 0 ? (
              <Alert severity="warning">No usable standard lots found for this titrant in inventory.</Alert>
            ) : !isPrimary && referenceOptions.length === 0 ? (
              <Alert severity="warning">No valid reference volumetric solution preparations found.</Alert>
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
        meaningStatement="I confirm that this titrant was standardized according to SOP and that the titration values entered are accurate."
        showComment
        comment={signatureComment}
        onCommentChange={setSignatureComment}
        onCancel={() => { if (!submitting) setSignatureOpen(false); }}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
