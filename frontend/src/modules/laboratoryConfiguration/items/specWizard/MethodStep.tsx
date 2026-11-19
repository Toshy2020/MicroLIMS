import { useEffect } from "react";
import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography
} from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import type {
  ResultBasis
} from "../../specifications/services/SpecificationService";
import { formatResultBasis } from "../components/copySpecifications";
import { IcpSpecFields } from "../components/IcpSpecFields";
import { ResidualSolventSpecFields } from "../components/ResidualSolventSpecFields";
import {
  TitrationSpecBasisFields,
  titrationBasisNeedsLabelClaim
} from "../components/TitrationSpecBasisFields";
import {
  HplcAssayMethodFields,
  RelatedSubstanceMethodFields
} from "./HplcMethodSpecFields";
import { resolveSpecMethodFlags, validateStep, type SpecFormModel } from "./specFormModel";
import {
  getHplcMethodLists,
  methodStepApplies,
  type SpecWizardLookupData
} from "./specHelpers";

interface MethodStepViewProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  lookup: SpecWizardLookupData;
}

function MethodStepView({ model, set, errors, lookup }: MethodStepViewProps) {
  const { t } = useTranslation("labConfig");
  const flags = resolveSpecMethodFlags(model);
  const currentDef = lookup.testDefs[model.testCode];
  const methodId = currentDef?.hplcMethodId;
  const loadingMethodAnalytes = methodId != null && lookup.loadingHplcMethodId === methodId;
  const { analytes: methodAnalytes, impurities: methodImpurities } = getHplcMethodLists(
    model,
    lookup.testDefs,
    lookup.hplcMethodById
  );

  const ensureHplcMethod = lookup.ensureHplcMethod;
  useEffect(() => {
    if (flags.isHplcMethodAssay && methodId != null) {
      ensureHplcMethod(methodId);
    }
  }, [flags.isHplcMethodAssay, methodId, ensureHplcMethod]);

  const icpElements = [...(currentDef?.icpElements ?? [])].sort(
    (x, y) => x.displayOrder - y.displayOrder
  );

  return (
    <Stack spacing={2.5}>
      {flags.isHplcMethodAssay &&
        (flags.isRelatedSubstances ? (
          <RelatedSubstanceMethodFields
            model={model}
            set={set}
            errors={errors}
            methodImpurities={methodImpurities}
            loadingMethodAnalytes={loadingMethodAnalytes}
          />
        ) : flags.isResidualSolvents ? (
          <Box data-field="hplcMethodAnalyteId">
            <ResidualSolventSpecFields
              methodAnalytes={methodAnalytes}
              loadingMethodAnalytes={loadingMethodAnalytes}
              selectedAnalyteId={model.hplcMethodAnalyteId}
              onAnalyteChange={(id, chosen) => {
                set({
                  hplcMethodAnalyteId: id,
                  ...(chosen ? { parameterName: chosen.name } : {})
                });
              }}
            />
            {errors.hplcMethodAnalyteId && (
              <FormHelperText error sx={{ ml: 1.5, mt: 0.5 }}>
                {errors.hplcMethodAnalyteId}
              </FormHelperText>
            )}
            {errors.resultBasis && (
              <FormHelperText error sx={{ ml: 1.5, mt: 0.5 }}>
                {errors.resultBasis}
              </FormHelperText>
            )}
          </Box>
        ) : (
          <HplcAssayMethodFields
            model={model}
            set={set}
            errors={errors}
            methodAnalytes={methodAnalytes}
            loadingMethodAnalytes={loadingMethodAnalytes}
            assignedTests={lookup.assignedTests}
          />
        ))}

      {flags.isTitration && (
        <Box data-field={errors.labelClaim ? "labelClaim" : "resultBasis"}>
          <TitrationSpecBasisFields
            finishedProduct={model.itemCategory === "FinishedProduct"}
            resultBasis={model.resultBasis}
            labelClaim={model.labelClaim}
            onBasisChange={(b) => set({ resultBasis: b })}
            onLabelClaimChange={(v) => set({ labelClaim: v })}
          />
          {errors.resultBasis && (
            <FormHelperText error sx={{ ml: 1.5, mt: 0.5 }}>
              {errors.resultBasis}
            </FormHelperText>
          )}
          {errors.labelClaim && (
            <FormHelperText error sx={{ ml: 1.5, mt: 0.5 }}>
              {errors.labelClaim}
            </FormHelperText>
          )}
        </Box>
      )}

      {flags.isUv && !flags.isUvLimit && (
        <Box sx={{ border: "1px dashed", borderColor: "divider", borderRadius: 1, p: 2 }}>
          <Typography
            variant="caption"
            sx={{
              fontWeight: 700,
              color: "primary.main",
              textTransform: "uppercase",
              display: "block",
              mb: 1.5
            }}
          >
            {t("specWizard.methodStep.uvHeading")}
          </Typography>
          <FormControl size="small" fullWidth>
            <InputLabel id="uv-spec-basis-label">{t("specWizard.methodStep.basisLabel")}</InputLabel>
            <Select
              name="resultBasis"
              labelId="uv-spec-basis-label"
              label={t("specWizard.methodStep.basisLabel")}
              value={model.resultBasis ?? ""}
              onChange={(e) => set({ resultBasis: e.target.value as ResultBasis | "" })}
            >
              <MenuItem value="">
                <em>{t("specWizard.methodStep.none")}</em>
              </MenuItem>
              <MenuItem value="PercentAsIs">{t("specWizard.methodStep.uvBasis.PercentAsIs")}</MenuItem>
              <MenuItem value="PercentDriedBasis">{t("specWizard.methodStep.uvBasis.PercentDriedBasis")}</MenuItem>
              <MenuItem value="PercentAnhydrousBasis">{t("specWizard.methodStep.uvBasis.PercentAnhydrousBasis")}</MenuItem>
            </Select>
          </FormControl>
        </Box>
      )}

      {flags.isIcpMethodAssay && (
        <Box data-field="icpTestElementId">
          <IcpSpecFields
            mode={currentDef?.icpMode ?? null}
            elements={icpElements}
            selectedElementId={model.icpTestElementId}
            onElementChange={(id, chosen) => {
              const patch: Partial<SpecFormModel> = { icpTestElementId: id };
              if (chosen) {
                const prevMatchesElement = icpElements.some(
                  (el) => el.symbol === model.parameterName
                );
                const prevMatchesTest = lookup.assignedTests.some(
                  (t) =>
                    t.displayName === model.parameterName || t.testCode === model.parameterName
                );
                if (!model.parameterName.trim() || prevMatchesElement || prevMatchesTest) {
                  patch.parameterName = chosen.symbol;
                }
              }
              set(patch);
            }}
          />
          {errors.icpTestElementId && (
            <FormHelperText error sx={{ ml: 1.5, mt: 0.5 }}>
              {errors.icpTestElementId}
            </FormHelperText>
          )}
        </Box>
      )}
    </Stack>
  );
}

function summarizeMethodStep(
  model: SpecFormModel,
  lookup: SpecWizardLookupData,
  t: TFunction<"labConfig">
): { label: string; value: string; field: string }[] {
  const flags = resolveSpecMethodFlags(model);
  const { analytes, impurities } = getHplcMethodLists(
    model,
    lookup.testDefs,
    lookup.hplcMethodById
  );
  const rows: { label: string; value: string; field: string }[] = [];

  if (flags.isHplcMethodAssay) {
    if (flags.isRelatedSubstances) {
      rows.push({
        field: "relatedSubstanceRow",
        label: t("specWizard.methodStep.summary.relatedSubstanceRow"),
        value: model.relatedSubstanceRow ? t(`specWizard.methodStep.relatedRowLabels.${model.relatedSubstanceRow}`) : ""
      });
      if (model.relatedSubstanceRow === "Named") {
        const imp = impurities.find((i) => i.id === model.hplcMethodImpurityId);
        rows.push({
          field: "hplcMethodImpurityId",
          label: t("specWizard.methodStep.summary.impurity"),
          value: imp?.name ?? (model.hplcMethodImpurityId ? String(model.hplcMethodImpurityId) : "")
        });
      }
      rows.push({ field: "resultBasis", label: t("specWizard.methodStep.summary.resultBasis"), value: t("specWizard.methodStep.summary.percentAsIs") });
    } else if (flags.isResidualSolvents) {
      const sol = analytes.find((a) => a.id === model.hplcMethodAnalyteId);
      rows.push({
        field: "hplcMethodAnalyteId",
        label: t("specWizard.methodStep.summary.solvent"),
        value: sol?.name ?? (model.hplcMethodAnalyteId ? String(model.hplcMethodAnalyteId) : "")
      });
      rows.push({ field: "resultBasis", label: t("specWizard.methodStep.summary.resultBasis"), value: t("specWizard.methodStep.summary.ppm") });
    } else {
      const analyte = analytes.find((a) => a.id === model.hplcMethodAnalyteId);
      rows.push({
        field: "hplcMethodAnalyteId",
        label: t("specWizard.methodStep.summary.analyte"),
        value:
          analyte?.name ?? (model.hplcMethodAnalyteId ? String(model.hplcMethodAnalyteId) : "")
      });
      rows.push({
        field: "resultBasis",
        label: t("specWizard.methodStep.summary.resultBasis"),
        value: formatResultBasis(model.resultBasis, false, t) ?? ""
      });
      if (model.resultBasis === "MgPerUnit") {
        rows.push({ field: "labelClaim", label: t("specWizard.methodStep.summary.labelClaim"), value: model.labelClaim });
        rows.push({
          field: "labelClaimUnit",
          label: t("specWizard.methodStep.summary.labelClaimUnit"),
          value: model.labelClaimUnit
        });
      }
    }
  }

  if (flags.isIcpMethodAssay) {
    const el = lookup.testDefs[model.testCode]?.icpElements?.find(
      (item) => item.id === model.icpTestElementId
    );
    rows.push({
      field: "icpTestElementId",
      label: t("specWizard.methodStep.summary.element"),
      value: el?.symbol ?? (model.icpTestElementId ? String(model.icpTestElementId) : "")
    });
  }

  if (flags.isTitration) {
    rows.push({
      field: "resultBasis",
      label: t("specWizard.methodStep.summary.resultBasis"),
      value: formatResultBasis(model.resultBasis, false, t) ?? ""
    });
    if (titrationBasisNeedsLabelClaim(model.resultBasis)) {
      rows.push({
        field: "labelClaim",
        label: t("specWizard.methodStep.summary.labelClaim"),
        value: model.labelClaim ? t("specWizard.methodStep.summary.mgValue", { value: model.labelClaim }) : ""
      });
    }
  }

  if (flags.isUv && !flags.isUvLimit) {
    rows.push({
      field: "resultBasis",
      label: t("specWizard.methodStep.summary.resultBasis"),
      value: model.resultBasis ? (formatResultBasis(model.resultBasis, false, t) ?? model.resultBasis) : t("specWizard.methodStep.none")
    });
  }

  return rows;
}

/**
 * Builds the Method & Basis wizard step (omitted when the test has no method/basis fields).
 */
export function createMethodStep(
  getLookup: () => SpecWizardLookupData,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): WizardStep<SpecFormModel> {
  return {
    key: "method",
    title: t("specWizard.methodStep.title"),
    applies: methodStepApplies,
    validate: (model) => validateStep("method", model, i18n.getFixedT(i18n.language, "labConfig")),
    render: (ctx) => <MethodStepView {...ctx} lookup={getLookup()} />,
    summary: (model) => summarizeMethodStep(model, getLookup(), t)
  };
}
