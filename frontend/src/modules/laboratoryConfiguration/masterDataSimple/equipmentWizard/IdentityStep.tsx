import { useMemo } from "react";
import type { TFunction } from "i18next";
import {
  Alert,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField
} from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { FieldHint, hintDescribedBy } from "../../../../components/FieldHint";
import { i18n, useTranslation } from "../../../../i18n";
import { getEquipmentHints } from "../hints";
import { MICRO_EQUIPMENT_TYPES, SET_POINT_TYPES, CONNECTION_TYPES, equipmentTypeLabel } from "./equipmentOptions";
import type { EquipmentWizardLookup } from "./equipmentLookup";
import { EquipmentFormModel, EquipmentModelContext, validateStep, withEquipmentType } from "./equipmentFormModel";

interface IdentityViewProps {
  model: EquipmentFormModel;
  set: (patch: Partial<EquipmentFormModel>) => void;
  errors: Record<string, string>;
  mode: "create" | "edit";
  lookup: EquipmentWizardLookup;
}

// Changing the type also drops settings the new type does not have, so a hidden
// value is never saved.
function changeType(model: EquipmentFormModel, nextType: string): EquipmentFormModel {
  const next = withEquipmentType(model, nextType);
  return {
    ...next,
    setPointTemperature: SET_POINT_TYPES.includes(nextType) ? next.setPointTemperature : "",
    connectionSettings: CONNECTION_TYPES.includes(nextType) ? next.connectionSettings : ""
  };
}

function IdentityStepView({ model, set, errors, mode, lookup }: IdentityViewProps) {
  const { t } = useTranslation("masterData");
  const hints = useMemo(() => getEquipmentHints(t), [t]);
  const showSection = mode !== "edit" && lookup.mySections.length > 1;
  const nameMissing = Boolean(errors.required) && !model.name.trim();
  const codeMissing = Boolean(errors.required) && !model.code.trim();
  const typeOptions = MICRO_EQUIPMENT_TYPES.some((mt) => mt.value === model.type)
    ? MICRO_EQUIPMENT_TYPES
    : [...MICRO_EQUIPMENT_TYPES, { value: model.type, label: equipmentTypeLabel(model.type) }];

  return (
    <Stack spacing={2}>
      {errors.required && (
        <Alert severity="error" role="alert">
          {errors.required}
        </Alert>
      )}
      <TextField
        size="small"
        name="name"
        label={t("equipmentWizard.identity.nameLabel")}
        placeholder={t("equipmentWizard.identity.namePlaceholder")}
        value={model.name}
        onChange={(e) => set({ name: e.target.value })}
        fullWidth
        required
        error={nameMissing}
      />
      <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
        <TextField
          size="small"
          name="code"
          label={t("equipmentWizard.identity.codeLabel")}
          placeholder={t("equipmentWizard.identity.codePlaceholder")}
          value={model.code}
          onChange={(e) => set({ code: e.target.value })}
          fullWidth
          required
          error={codeMissing}
          slotProps={{ input: { "aria-describedby": hintDescribedBy("equipment-code") } }}
        />
        <FieldHint id="equipment-code" label={t("equipmentWizard.identity.codeHintLabel")} hint={hints.equipmentCode} />
      </Stack>
      <FormControl fullWidth required size="small">
        <InputLabel id="equipment-type-select-label">{t("equipmentWizard.identity.typeLabel")}</InputLabel>
        <Select
          labelId="equipment-type-select-label"
          label={t("equipmentWizard.identity.typeLabel")}
          name="type"
          value={model.type}
          onChange={(e) => set(changeType(model, e.target.value))}
        >
          {typeOptions.map((opt) => (
            <MenuItem key={opt.value} value={opt.value}>
              {t(`equipmentTypes.${opt.value}`, { defaultValue: opt.label })}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      <TextField
        size="small"
        name="vendor"
        label={t("equipmentWizard.identity.vendorLabel")}
        placeholder={t("equipmentWizard.identity.vendorPlaceholder")}
        value={model.vendor}
        onChange={(e) => set({ vendor: e.target.value })}
        fullWidth
        slotProps={{ htmlInput: { maxLength: 100 } }}
      />
      {showSection && (
        <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
          <FormControl size="small" fullWidth required sx={{ flex: 1 }} error={Boolean(errors.section)}>
            <InputLabel id="equipment-section-label">{t("equipmentWizard.identity.sectionLabel")}</InputLabel>
            <Select<number | "">
              labelId="equipment-section-label"
              label={t("equipmentWizard.identity.sectionLabel")}
              name="section"
              value={model.sectionId}
              onChange={(e) => set({ sectionId: e.target.value === "" ? "" : Number(e.target.value) })}
              inputProps={{ "aria-label": t("equipmentWizard.identity.sectionAria"), "aria-describedby": hintDescribedBy("equipment-section") }}
            >
              <MenuItem value="">
                <em>{t("equipmentWizard.identity.sectionPlaceholder")}</em>
              </MenuItem>
              {lookup.mySections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {t("equipmentWizard.identity.sectionOption", { name: s.sectionName, department: s.departmentName })}
                </MenuItem>
              ))}
            </Select>
            {errors.section && <FormHelperText>{errors.section}</FormHelperText>}
          </FormControl>
          <FieldHint id="equipment-section" label={t("equipmentWizard.identity.sectionAria")} hint={hints.laboratorySection} />
        </Stack>
      )}
      <TextField
        size="small"
        name="location"
        label={t("equipmentWizard.identity.locationLabel")}
        placeholder={t("equipmentWizard.identity.locationPlaceholder")}
        value={model.location}
        onChange={(e) => set({ location: e.target.value })}
        fullWidth
      />
    </Stack>
  );
}

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export function createIdentityStep(
  getLookup: () => EquipmentWizardLookup,
  getContext: () => EquipmentModelContext,
  getT: () => TFunction = defaultT
): WizardStep<EquipmentFormModel> {
  return {
    key: "identity",
    title: getT()("equipmentWizard.identity.stepTitle"),
    validate: (model) => validateStep("identity", model, getContext(), getT()),
    render: ({ model, set, errors, mode }) => (
      <IdentityStepView model={model} set={set} errors={errors} mode={mode} lookup={getLookup()} />
    ),
    summary: (model) => {
      const t = getT();
      const lookup = getLookup();
      const section = lookup.mySections.find((s) => s.sectionId === model.sectionId);
      const rows = [
        { label: t("equipmentWizard.identity.summary.name"), value: model.name.trim() || t("common.dash"), field: "name" },
        { label: t("equipmentWizard.identity.summary.code"), value: model.code.trim() || t("common.dash"), field: "code" },
        { label: t("equipmentWizard.identity.summary.type"), value: t(`equipmentTypes.${model.type}`, { defaultValue: equipmentTypeLabel(model.type) }), field: "type" },
        { label: t("equipmentWizard.identity.summary.vendor"), value: model.vendor.trim() || t("common.dash"), field: "vendor" }
      ];
      if (lookup.mySections.length > 1) {
        rows.push({ label: t("equipmentWizard.identity.summary.section"), value: section?.sectionName ?? t("common.dash"), field: "section" });
      }
      rows.push({ label: t("equipmentWizard.identity.summary.location"), value: model.location.trim() || t("common.dash"), field: "location" });
      return rows;
    }
  };
}
