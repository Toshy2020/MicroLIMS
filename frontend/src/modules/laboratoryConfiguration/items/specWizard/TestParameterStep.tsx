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
          The test definitions could not be loaded, so test-specific fields may be missing. Close and reopen this dialog to try again.
        </Alert>
      )}

      {!lookup.isTestDisabled && lookup.selectableAssignedTests.length === 0 && (
        <Alert severity="warning">
          None of this item&rsquo;s assigned tests belong to your laboratory. Ask that test&rsquo;s Section Head to add
          the specification.
        </Alert>
      )}

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
        <FormControl
          size="small"
          fullWidth
          disabled={lookup.isTestDisabled}
          error={Boolean(errors.testCode)}
        >
          <InputLabel id="assigned-test-label">Assigned Test *</InputLabel>
          <Select
            name="testCode"
            labelId="assigned-test-label"
            label="Assigned Test *"
            value={model.testCode}
            onChange={(e) => handleTestChange(e.target.value)}
          >
            {lookup.selectableAssignedTests.map((t) => (
              <MenuItem key={t.testCode} value={t.testCode}>
                {formatAssignedTestName(lookup.assignedTests, t.testCode)}
              </MenuItem>
            ))}
          </Select>
          {errors.testCode && <FormHelperText>{errors.testCode}</FormHelperText>}
        </FormControl>

        <TextField
          size="small"
          name="parameterName"
          label="Parameter Name *"
          value={model.parameterName}
          onChange={(e) => set({ parameterName: e.target.value })}
          error={Boolean(errors.parameterName)}
          helperText={errors.parameterName}
          fullWidth
          placeholder="e.g. Assay (HPLC) or Impurity A"
        />
      </Box>

      {flags.supportsProductionStage && (
        <FormControl size="small" fullWidth error={Boolean(errors.productionStageRole)}>
          <InputLabel id="production-stage-label">Production Stage</InputLabel>
          <Select
            name="productionStageRole"
            labelId="production-stage-label"
            label="Production Stage"
            value={model.productionStageRole}
            onChange={(e) =>
              set({ productionStageRole: e.target.value as ProductionStageRole | "" })
            }
          >
            <MenuItem value="">All stages</MenuItem>
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
            A sample at a stage that has its own specification for this test uses it; every other sample uses the
            &quot;All stages&quot; specification.
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
              label="Grade"
              error={Boolean(errors.grades)}
              helperText={
                errors.grades ||
                "Pick every grade these limits apply to (BP, USP, In-house or your own, max 20 characters). Leave empty for an ungraded specification."
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
  getLookup: () => SpecWizardLookupData
): WizardStep<SpecFormModel> {
  return {
    key: "test",
    title: "Test & Parameter",
    validate: (model) => validateStep("test", model),
    render: (ctx) => <TestParameterStepView {...ctx} lookup={getLookup()} />,
    summary: (model) => {
      const lookup = getLookup();
      const flags = resolveSpecMethodFlags(model);
      const rows = [
        {
          field: "testCode",
          label: "Assigned test",
          value: formatAssignedTestName(lookup.assignedTests, model.testCode)
        },
        {
          field: "parameterName",
          label: "Parameter name",
          value: model.parameterName.trim()
        }
      ];
      if (flags.supportsProductionStage) {
        rows.push({
          field: "productionStageRole",
          label: "Production stage",
          value: productionStageRoleLabel(model.productionStageRole || null)
        });
      }
      if (flags.supportsGrade) {
        rows.push({
          field: "grades",
          label: "Grades",
          value: model.grades.length > 0 ? model.grades.join(", ") : "Ungraded"
        });
      }
      return rows;
    }
  };
}
