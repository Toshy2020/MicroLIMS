import { useEffect } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import {
  GRADE_SUGGESTIONS,
  PRODUCTION_STAGE_ROLE_OPTIONS,
  productionStageRoleLabel,
  type ProductionStageRole
} from "../../specifications/services/SpecificationService";
import { resolveSpecMethodFlags, validateStep, type SpecFormModel } from "./specFormModel";
import {
  buildTestChangePatch,
  formatAssignedTestName,
  type SpecWizardLookupData
} from "./specHelpers";

interface TestParameterStepViewProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  mode: "create" | "edit";
  lookup: SpecWizardLookupData;
}

function TestParameterStepView({
  model,
  set,
  errors,
  mode,
  lookup
}: TestParameterStepViewProps) {
  const { t } = useTranslation("labConfig");
  const flags = resolveSpecMethodFlags(model);
  const currentDef = lookup.testDefs[model.testCode];
  const methodId = currentDef?.hplcMethodId;
  const loadedMethod = methodId != null ? lookup.hplcMethodById[methodId] : undefined;

  useEffect(() => {
    if (methodId != null) {
      lookup.ensureHplcMethod(methodId);
    }
  }, [methodId, lookup]);

  useEffect(() => {
    if (!loadedMethod) return;
    const firstAnalyteId = loadedMethod.analytes?.[0]?.id ?? null;
    if (
      model.hplcMethodResultMode === loadedMethod.resultMode &&
      model.defaultHplcAnalyteId === firstAnalyteId
    ) {
      return;
    }
    const patch: Partial<SpecFormModel> = {
      hplcMethodResultMode: loadedMethod.resultMode,
      defaultHplcAnalyteId: firstAnalyteId
    };
    if (loadedMethod.resultMode === "ResidualSolvents") {
      patch.resultBasis = "Ppm";
      patch.limitType = "NotMoreThan";
      patch.unit = "ppm";
    } else if (loadedMethod.resultMode === "RelatedSubstances") {
      patch.resultBasis = "PercentAsIs";
      patch.limitType = "NotMoreThan";
      patch.unit = "%";
      if (firstAnalyteId != null && !model.hplcMethodAnalyteId) {
        patch.hplcMethodAnalyteId = firstAnalyteId;
      }
    }
    set(patch);
  }, [
    loadedMethod,
    model.hplcMethodResultMode,
    model.defaultHplcAnalyteId,
    model.hplcMethodAnalyteId,
    set
  ]);

  useEffect(() => {
    if (!currentDef) return;
    if (
      model.workflowType === currentDef.workflowType &&
      model.equationType === currentDef.equationType &&
      (model.uvCalculationMode ?? null) === (currentDef.uvCalculationMode ?? null) &&
      (model.icpMode ?? null) === (currentDef.icpMode ?? null)
    ) {
      return;
    }
    if (mode === "create") {
      set(
        buildTestChangePatch(
          model.testCode,
          lookup.assignedTests,
          lookup.testDefs,
          lookup.workflowTypeByCode,
          lookup.hplcMethodById
        )
      );
    } else {
      set({
        workflowType: currentDef.workflowType,
        equationType: currentDef.equationType,
        uvCalculationMode: currentDef.uvCalculationMode ?? null,
        icpMode: currentDef.icpMode ?? null
      });
    }
  }, [
    currentDef,
    mode,
    model.testCode,
    model.workflowType,
    model.equationType,
    model.uvCalculationMode,
    model.icpMode,
    lookup,
    set
  ]);

  const handleTestChange = (newCode: string) => {
    set(
      buildTestChangePatch(
        newCode,
        lookup.assignedTests,
        lookup.testDefs,
        lookup.workflowTypeByCode,
        lookup.hplcMethodById
      )
    );
  };

  return (
    <Stack spacing={2.5}>
      {lookup.testDefsFailed && (
        <Alert severity="error">
          {t("specWizard.testStep.testDefsFailed")}
        </Alert>
      )}

      {!lookup.isTestDisabled && lookup.selectableAssignedTests.length === 0 && (
        <Alert severity="warning">
          {t("specWizard.testStep.noOwnLabTests")}
        </Alert>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
        <FormControl
          size="small"
          fullWidth
          disabled={lookup.isTestDisabled}
          error={Boolean(errors.testCode)}
        >
          <InputLabel id="assigned-test-label">{t("specWizard.testStep.assignedTestLabel")}</InputLabel>
          <Select
            name="testCode"
            labelId="assigned-test-label"
            label={t("specWizard.testStep.assignedTestLabel")}
            value={model.testCode}
            onChange={(e) => handleTestChange(e.target.value)}
          >
            {lookup.selectableAssignedTests.map((tOpt) => (
              <MenuItem key={tOpt.testCode} value={tOpt.testCode}>
                {formatAssignedTestName(lookup.assignedTests, tOpt.testCode)}
              </MenuItem>
            ))}
          </Select>
          {errors.testCode && <FormHelperText>{errors.testCode}</FormHelperText>}
        </FormControl>

        <TextField
          size="small"
          name="parameterName"
          label={t("specWizard.testStep.parameterNameLabel")}
          value={model.parameterName}
          onChange={(e) => set({ parameterName: e.target.value })}
          error={Boolean(errors.parameterName)}
          helperText={errors.parameterName}
          fullWidth
          placeholder={t("specWizard.testStep.parameterNamePlaceholder")}
        />
      </Box>

      {flags.supportsProductionStage && (
        <FormControl size="small" fullWidth error={Boolean(errors.productionStageRole)}>
          <InputLabel id="production-stage-label">{t("specWizard.testStep.productionStageLabel")}</InputLabel>
          <Select
            name="productionStageRole"
            labelId="production-stage-label"
            label={t("specWizard.testStep.productionStageLabel")}
            value={model.productionStageRole}
            onChange={(e) =>
              set({ productionStageRole: e.target.value as ProductionStageRole | "" })
            }
          >
            <MenuItem value="">{t("specWizard.testStep.allStages")}</MenuItem>
            {PRODUCTION_STAGE_ROLE_OPTIONS.map((o) => (
              <MenuItem key={o.value} value={o.value}>
                {o.label}
              </MenuItem>
            ))}
          </Select>
          {errors.productionStageRole && (
            <FormHelperText>{errors.productionStageRole}</FormHelperText>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
            {t("specWizard.testStep.productionStageHelper")}
          </Typography>
        </FormControl>
      )}

      {flags.supportsGrade && (
        <Autocomplete
          multiple
          freeSolo
          size="small"
          options={GRADE_SUGGESTIONS}
          value={model.grades}
          onChange={(_, v) => {
            const unique: string[] = [];
            for (const raw of v) {
              const g = raw.trim().slice(0, 20);
              if (g && !unique.some((u) => u.toLowerCase() === g.toLowerCase())) unique.push(g);
            }
            set({ grades: unique });
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              name="grades"
              label={t("specWizard.testStep.gradeLabel")}
              error={Boolean(errors.grades)}
              helperText={
                errors.grades ||
                t("specWizard.testStep.gradeHelper")
              }
              slotProps={{
                ...params.slotProps,
                htmlInput: { ...params.slotProps.htmlInput, maxLength: 20 }
              }}
            />
          )}
        />
      )}
    </Stack>
  );
}

/**
 * Builds the Test & Parameter wizard step.
 */
export function createTestParameterStep(
  getLookup: () => SpecWizardLookupData,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): WizardStep<SpecFormModel> {
  return {
    key: "test",
    title: t("specWizard.testStep.title"),
    validate: (model) => validateStep("test", model),
    render: (ctx) => <TestParameterStepView {...ctx} lookup={getLookup()} />,
    summary: (model) => {
      const lookup = getLookup();
      const flags = resolveSpecMethodFlags(model);
      const rows = [
        {
          field: "testCode",
          label: t("specWizard.testStep.summary.assignedTest"),
          value: formatAssignedTestName(lookup.assignedTests, model.testCode)
        },
        {
          field: "parameterName",
          label: t("specWizard.testStep.summary.parameterName"),
          value: model.parameterName.trim()
        }
      ];
      if (flags.supportsProductionStage) {
        rows.push({
          field: "productionStageRole",
          label: t("specWizard.testStep.summary.productionStage"),
          value: productionStageRoleLabel(model.productionStageRole || null)
        });
      }
      if (flags.supportsGrade) {
        rows.push({
          field: "grades",
          label: t("specWizard.testStep.summary.grades"),
          value: model.grades.length > 0 ? model.grades.join(", ") : t("specWizard.testStep.summary.ungraded")
        });
      }
      return rows;
    }
  };
}
