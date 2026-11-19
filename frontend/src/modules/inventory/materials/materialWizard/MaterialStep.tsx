import { useCallback, useEffect, useMemo, useRef } from "react";
import { Box, FormControl, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import { i18n, useTranslation } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem, MaterialType, MaterialUnit } from "../types/materialTypes";
import { typeText, wizardText as tw } from "./wizardText";
import { MaterialKindFields } from "./MaterialKindFields";
import { StepErrorAlert } from "./StepErrorAlert";
import type { MaterialLookups } from "./materialLookups";
import {
  newLotTypesFor,
  validateStep,
  withMaterialType,
  type MaterialFormModel,
  type MaterialModelContext
} from "./materialFormModel";
import { categoryPatch, deriveMaterialView } from "./materialSelections";

interface MaterialStepProps {
  model: MaterialFormModel;
  set: (patch: Partial<MaterialFormModel>) => void;
  errors: Record<string, string>;
  editing: MaterialItem | null;
  lookups: MaterialLookups;
}

const isStandard = (type: MaterialType) => type === "ReferenceStandard" || type === "PrimaryStandard";

function MaterialStepView({ model, set, errors, editing, lookups }: MaterialStepProps) {
  const { t } = useTranslation("inventory");
  const modelRef = useRef(model);
  modelRef.current = model;
  const { setDemand, initialTypeApplied, typeOptions } = lookups;

  const activeSectionId = editing ? editing.sectionId : model.sectionId !== "" ? Number(model.sectionId) : undefined;
  const view = deriveMaterialView(model, editing, lookups);
  const newLotTypes = useMemo(() => newLotTypesFor(typeOptions), [typeOptions]);
  // An existing lot whose type is not in its lab's list still shows its current type.
  const typeSelectOptions: MaterialType[] =
    editing && !newLotTypes.includes(editing.materialType) ? [...newLotTypes, editing.materialType] : newLotTypes;

  const changeType = useCallback(
    async (type: MaterialType) => {
      let defaultUnit: MaterialUnit | null = null;
      try {
        defaultUnit = (await MaterialService.getDefaultUnit(type)) as MaterialUnit;
      } catch {
        // Keep the unit already chosen.
      }
      set(withMaterialType(modelRef.current, type, defaultUnit, editing !== null));
    },
    [set, editing]
  );

  useEffect(() => {
    setDemand({ sectionId: activeSectionId ?? null, type: model.materialType });
  }, [setDemand, activeSectionId, model.materialType]);

  // When creating, the initial type is the first type of the lab, not "DehydratedMedia".
  useEffect(() => {
    if (editing || !typeOptions || newLotTypes.length === 0) return;
    if (!initialTypeApplied.current) {
      initialTypeApplied.current = true;
      void changeType(newLotTypes[0]);
    } else if (!newLotTypes.includes(model.materialType)) {
      void changeType(newLotTypes[0]);
    }
  }, [typeOptions, newLotTypes, editing, model.materialType, changeType, initialTypeApplied]);

  const nameLocked = model.materialType === "DehydratedMedia" || Boolean(model.materialMasterEntryId) || view.nameFromEntryOnly || view.nameFromStrain;
  const fromHelper = model.materialMasterEntryId || view.nameFromEntryOnly
    ? t("wizard.material.fromMaster")
    : view.nameFromStrain
      ? t("wizard.material.fromOrganism")
      : undefined;

  return (
    <>
      <LoadFailuresAlert failed={lookups.failed} sx={{ mb: 2.5 }} />
      <StepErrorAlert errors={errors} />
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        {view.editingOtherMaterial ? (
          <TextField
            size="small"
            label={t("wizard.material.type")}
            value={view.storedTypeLabel}
            disabled
            helperText={view.editingLegacyLot ? t("wizard.material.legacyHelper") : t("wizard.material.keepsCategory")}
          />
        ) : (
          <FormControl
            size="small"
            fullWidth
            required
            sx={{ gridColumn: { xs: "1", sm: model.materialType === "DehydratedMedia" ? "span 2" : "1" } }}
          >
            <InputLabel id="material-type-label">{t("wizard.material.type")}</InputLabel>
            <Select<MaterialType>
              labelId="material-type-label"
              label={t("wizard.material.type")}
              name="materialType"
              value={typeSelectOptions.includes(model.materialType) ? model.materialType : ""}
              onChange={(e) => void changeType(e.target.value as MaterialType)}
            >
              {typeSelectOptions.map((ty) => (
                <MenuItem key={ty} value={ty}>
                  {typeText(ty)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {!editing && lookups.sections.length > 1 && (
          <FormControl size="small" fullWidth required error={Boolean(errors.section)} sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="material-section-label">{t("wizard.material.section")}</InputLabel>
            <Select<number | "">
              labelId="material-section-label"
              label={t("wizard.material.section")}
              name="section"
              value={model.sectionId}
              onChange={(e) => {
                const sectionId = e.target.value === "" ? "" : Number(e.target.value);
                // Categories are offered per lab.
                set({ sectionId, ...(model.materialType === "Other" ? categoryPatch(model, "", lookups.categories) : {}) });
              }}
              inputProps={{ "aria-label": t("wizard.material.section") }}
            >
              <MenuItem value="">
                <em>{t("wizard.material.selectSection")}</em>
              </MenuItem>
              {lookups.sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName} ({s.departmentName})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        <MaterialKindFields
          model={model}
          set={set}
          errors={errors}
          editing={editing}
          lookups={lookups}
          view={view}
          activeSectionId={activeSectionId}
        />

        <TextField
          size="small"
          required
          name="materialName"
          label={t("wizard.material.name")}
          placeholder={t("wizard.material.nameExample")}
          value={model.materialName}
          onChange={(e) => set({ materialName: e.target.value })}
          disabled={nameLocked}
          error={Boolean(errors.materialName)}
          helperText={fromHelper}
        />
        <TextField
          size="small"
          name="manufacturerName"
          label={t("wizard.material.manufacturer")}
          placeholder={t("wizard.material.manufacturerExample")}
          value={model.manufacturerName}
          onChange={(e) => set({ manufacturerName: e.target.value })}
        />
        <TextField
          size="small"
          name="code"
          label={model.materialType === "DehydratedMedia" ? t("wizard.material.mediaCode") : t("wizard.material.catalogCode")}
          placeholder={model.materialType === "DehydratedMedia" ? t("wizard.material.mediaCodeExample") : t("wizard.material.catalogExample")}
          value={model.code}
          onChange={(e) => set({ code: e.target.value })}
          disabled={nameLocked}
          helperText={fromHelper}
        />

        {isStandard(model.materialType) && (
          <TextField
            size="small"
            required
            name="purity"
            label={t("wizard.material.purity")}
            placeholder={t("wizard.material.purityExample")}
            type="number"
            value={model.purity}
            onChange={(e) => set({ purity: e.target.value })}
            error={Boolean(errors.purity)}
            slotProps={{ htmlInput: { step: "0.001", min: "0.001", max: "100" } }}
            helperText={t("wizard.material.purityHelp")}
          />
        )}
        {model.materialType === "ReferenceStandard" && (
          <TextField
            size="small"
            name="moisturePercent"
            label={t("wizard.material.moisture")}
            placeholder={t("wizard.material.moistureExample")}
            type="number"
            value={model.moisturePercent}
            onChange={(e) => set({ moisturePercent: e.target.value })}
            error={Boolean(errors.moisturePercent)}
            slotProps={{ htmlInput: { step: "0.001", min: "0", max: "99.999" } }}
            helperText={t("wizard.material.moistureHelp")}
          />
        )}
      </Box>
    </>
  );
}

export function createMaterialStep(
  getEditing: () => MaterialItem | null,
  getLookups: () => MaterialLookups,
  getContext: (model: MaterialFormModel) => MaterialModelContext
): WizardStep<MaterialFormModel> {
  return {
    key: "material",
    title: tw("wizard.steps.material"),
    validate: (model) => validateStep("material", model, getContext(model), i18n.getFixedT(i18n.language, "inventory")),
    render: ({ model, set, errors }) => (
      <MaterialStepView model={model} set={set} errors={errors} editing={getEditing()} lookups={getLookups()} />
    ),
    summary: (model) => {
      const editing = getEditing();
      const lookups = getLookups();
      const view = deriveMaterialView(model, editing, lookups);
      const section = lookups.sections.find((s) => s.sectionId === model.sectionId);
      const rows: { label: string; value: string; field: string }[] = [
        {
          label: tw("wizard.material.type"),
          value: editing && view.editingOtherMaterial ? view.storedTypeLabel : typeText(model.materialType),
          field: "materialType"
        }
      ];
      if (lookups.sections.length > 1 && !editing) {
        rows.push({ label: tw("wizard.material.section"), value: section?.sectionName ?? "—", field: "section" });
      }
      if (model.materialType === "Other" && !editing) {
        rows.push({ label: tw("wizard.material.category"), value: view.selectedCategory?.name ?? "—", field: "categoryId" });
      }
      rows.push(
        { label: tw("wizard.material.name"), value: model.materialName.trim() || "—", field: "materialName" },
        { label: tw("wizard.material.code"), value: model.code.trim() || "—", field: "code" },
        { label: tw("wizard.material.manufacturer"), value: model.manufacturerName.trim() || "—", field: "manufacturerName" }
      );
      if (isStandard(model.materialType)) {
        rows.push({ label: tw("wizard.material.purity"), value: model.purity !== "" ? String(model.purity) : "—", field: "purity" });
      }
      if (model.materialType === "ReferenceStandard") {
        rows.push({
          label: tw("wizard.material.moisture"),
          value: model.moisturePercent !== "" ? String(model.moisturePercent) : "—",
          field: "moisturePercent"
        });
      }
      return rows;
    }
  };
}
