import {
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
import type { LimitType } from "../../specifications/services/SpecificationService";
import { formatLimitCell } from "../components/copySpecifications";
import { CountTieredLimits } from "./limits/CountTieredLimits";
import { NumericLimits } from "./limits/NumericLimits";
import { QualitativeLimits } from "./limits/QualitativeLimits";
import { StageLimits } from "./limits/StageLimits";
import { resolveSpecMethodFlags, validateStep, type SpecFormModel } from "./specFormModel";
import {
  LIMIT_TYPE_OPTIONS,
  modelToDisplaySpec,
  type SpecWizardLookupData
} from "./specHelpers";

interface LimitStepViewProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  lookup: SpecWizardLookupData;
}

function LimitStepView({ model, set, errors, lookup }: LimitStepViewProps) {
  const flags = resolveSpecMethodFlags(model);
  const currentDef = lookup.testDefs[model.testCode];
  const wf = currentDef?.workflowType || lookup.workflowTypeByCode[model.testCode];

  const isDissolution = wf === "Dissolution" || model.limitType === "DissolutionQ";
  const isDisintegration = wf === "Disintegration" || model.limitType === "DisintegrationTime";
  const isWeightVariation = wf === "WeightVariation" || model.limitType === "WeightVariation";

  const availableOptions = isWeightVariation
    ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "WeightVariation")
    : isDisintegration
    ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "DisintegrationTime")
    : isDissolution
    ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "DissolutionQ")
    : flags.isUv
    ? flags.isUvLimit
      ? LIMIT_TYPE_OPTIONS.filter((opt) => opt.value === "Qualitative")
      : LIMIT_TYPE_OPTIONS.filter((opt) =>
          ["Range", "NotMoreThan", "NotLessThan"].includes(opt.value)
        )
    : flags.isHplcMethodAssay || flags.isIcpMethodAssay || flags.isTitration
    ? LIMIT_TYPE_OPTIONS.filter((opt) =>
        ["Range", "NotMoreThan", "NotLessThan", "TargetWithTolerance"].includes(opt.value)
      )
    : LIMIT_TYPE_OPTIONS.filter(
        (opt) =>
          opt.value !== "DissolutionQ" &&
          opt.value !== "DisintegrationTime" &&
          opt.value !== "WeightVariation"
      );

  const handleLimitTypeChange = (newType: LimitType) => {
    const patch: Partial<SpecFormModel> = { limitType: newType };
    if (newType !== "CountTiered") {
      patch.dilutionFactor = "";
    }
    if (newType === "MultiStage" && model.stages.length === 0) {
      patch.stages = [{ stageNumber: 1, stageLabel: "Stage 1", acceptanceCriteriaText: "" }];
    }
    set(patch);
  };

  const renderLimitFields = () => {
    switch (model.limitType) {
      case "Range":
      case "NotMoreThan":
      case "NotLessThan":
      case "TargetWithTolerance":
        return <NumericLimits model={model} set={set} errors={errors} flags={flags} />;
      case "CountTiered":
        return <CountTieredLimits model={model} set={set} errors={errors} flags={flags} />;
      case "Qualitative":
      case "PresenceAbsence":
        return <QualitativeLimits model={model} set={set} errors={errors} flags={flags} />;
      case "MultiStage":
      case "DissolutionQ":
      case "DisintegrationTime":
      case "WeightVariation":
        return <StageLimits model={model} set={set} errors={errors} flags={flags} />;
      default:
        return null;
    }
  };

  return (
    <Stack spacing={2.5}>
      {flags.isRelatedSubstances ? (
        <TextField
          size="small"
          label="Limit Type *"
          value="NMT (Not More Than)"
          slotProps={{ input: { readOnly: true } }}
          helperText="Fixed limit type for related substances"
          fullWidth
        />
      ) : flags.isResidualSolvents ? (
        <TextField
          size="small"
          label="Limit Type *"
          value="NMT (Not More Than)"
          slotProps={{ input: { readOnly: true } }}
          helperText="Fixed limit type for residual solvents"
          fullWidth
        />
      ) : flags.isIcpElementalImpurities ? null : (
        <FormControl size="small" fullWidth error={Boolean(errors.limitType)}>
          <InputLabel id="limit-type-label">Limit Type *</InputLabel>
          <Select
            name="limitType"
            labelId="limit-type-label"
            label="Limit Type *"
            value={model.limitType}
            onChange={(e) => handleLimitTypeChange(e.target.value as LimitType)}
          >
            {availableOptions.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
          {errors.limitType && <FormHelperText>{errors.limitType}</FormHelperText>}
        </FormControl>
      )}

      <Box
        sx={{
          border: "1px dashed",
          borderColor: "divider",
          borderRadius: 1,
          p: 2,
          bgcolor: "action.hover"
        }}
      >
        <Typography
          variant="caption"
          sx={{
            fontWeight: 700,
            letterSpacing: "0.5px",
            color: "text.secondary",
            textTransform: "uppercase",
            display: "block",
            mb: 1.5
          }}
        >
          LIMIT DEFINITION &middot; CHANGES WITH TYPE ABOVE
        </Typography>

        {renderLimitFields()}
      </Box>
    </Stack>
  );
}

function primaryLimitField(limitType: LimitType): string {
  switch (limitType) {
    case "Range":
    case "NotLessThan":
    case "DissolutionQ":
      return "lowerLimit";
    case "NotMoreThan":
    case "DisintegrationTime":
      return "upperLimit";
    case "TargetWithTolerance":
      return "target";
    case "CountTiered":
      return "specLimit";
    case "Qualitative":
      return "expectedResultText";
    case "PresenceAbsence":
      return "expectedState";
    case "WeightVariation":
      return "dosageForm";
    default:
      return "limit";
  }
}

/**
 * Builds the Limits wizard step.
 */
export function createLimitStep(
  getLookup: () => SpecWizardLookupData
): WizardStep<SpecFormModel> {
  return {
    key: "limit",
    title: "Limits",
    validate: (model) => validateStep("limit", model),
    render: (ctx) => <LimitStepView {...ctx} lookup={getLookup()} />,
    summary: (model) => {
      const displaySpec = modelToDisplaySpec(model);
      const limitTypeLabel =
        LIMIT_TYPE_OPTIONS.find((opt) => opt.value === displaySpec.limitType)?.label ??
        String(displaySpec.limitType ?? model.limitType);

      const rows = [
        { field: "limitType", label: "Limit type", value: limitTypeLabel },
        {
          field: primaryLimitField(model.limitType),
          label: "Limit",
          value: formatLimitCell(displaySpec)
        }
      ];

      if (model.limitType === "Range") {
        rows.push({
          field: "lowerInclusive",
          label: "Boundary inclusivity",
          value: `${model.lowerInclusive ? "≥ lower" : "> lower"}, ${model.upperInclusive ? "≤ upper" : "< upper"}`
        });
      }

      if (model.limitType === "DissolutionQ") {
        rows.push({
          field: "labelClaim",
          label: "Label claim",
          value: model.labelClaim ? `${model.labelClaim} mg` : ""
        });
      }

      if (model.limitType === "MultiStage") {
        rows.push({
          field: "stages",
          label: "Stages",
          value: model.stages
            .map((s) => `${s.stageLabel}: ${s.acceptanceCriteriaText}`)
            .join(" · ")
        });
      }

      return rows;
    }
  };
}
