import { Autocomplete, Box, FormControl, FormHelperText, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import { MediaProductPicker } from "../../../../components/MediaProductPicker";
import type { MaterialMasterEntry } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import type { MaterialItem } from "../types/materialTypes";
import type { MaterialLookups } from "./materialLookups";
import { isMasterLinkedType, type MaterialFormModel } from "./materialFormModel";
import {
  categoryPatch,
  masterEntryPatch,
  mediaProductPatch,
  strainPatch,
  type MaterialView
} from "./materialSelections";

interface KindFieldsProps {
  model: MaterialFormModel;
  set: (patch: Partial<MaterialFormModel>) => void;
  errors: Record<string, string>;
  editing: MaterialItem | null;
  lookups: MaterialLookups;
  view: MaterialView;
  activeSectionId: number | undefined;
}

const fullRow = { gridColumn: { xs: "1", sm: "span 2" } };

function masterEntryKey(type: MaterialFormModel["materialType"]): string {
  if (type === "Chemical" || type === "Indicator" || type === "PrimaryStandard" || type === "Other") return type;
  return "ReferenceStandard";
}

// What is being received: the configured media product, the reference strain, or the category and
// master entry that carry the material's name, code and rules.
export function MaterialKindFields({ model, set, errors, editing, lookups, view, activeSectionId }: KindFieldsProps) {
  const { t } = useTranslation("inventory");
  const { selectedCategory, selectedMasterEntry } = view;
  const showMasterEntry = isMasterLinkedType(model.materialType) && !(model.materialType === "Other" && editing);

  return (
    <>
      {model.materialType === "DehydratedMedia" && (
        <Box sx={fullRow}>
          <MediaProductPicker
            value={model.mediaProductId}
            onChange={(id, product) => set(mediaProductPatch(id, product, editing))}
            required
            error={Boolean(errors.mediaProductId)}
            helperText={t("wizard.kind.mediaHelp")}
          />
        </Box>
      )}

      {model.materialType === "Other" && editing && editing.materialMasterEntryId && (
        <>
          <TextField size="small" label={t("wizard.kind.category")} value={editing.categoryName ?? "—"} disabled />
          <TextField
            size="small"
            label={t("wizard.kind.masterEntry")}
            value={`${editing.materialMasterEntryCode ?? editing.code ?? ""}: ${editing.materialName}`}
            disabled
          />
        </>
      )}

      {model.materialType === "Other" && !editing && (
        <FormControl size="small" fullWidth required error={Boolean(errors.categoryId)} sx={fullRow}>
          <InputLabel id="material-category-label">{t("wizard.kind.category")}</InputLabel>
          <Select<number | "">
            labelId="material-category-label"
            label={t("wizard.kind.category")}
            name="categoryId"
            value={selectedCategory ? selectedCategory.id : ""}
            onChange={(e) => set(categoryPatch(model, e.target.value === "" ? "" : Number(e.target.value), lookups.categories))}
            disabled={activeSectionId == null || lookups.categoriesLoading}
          >
            {lookups.categories.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </Select>
          <FormHelperText>
            {activeSectionId == null
              ? t("wizard.kind.chooseSection")
              : lookups.categories.length === 0 && !lookups.categoriesLoading
                ? t("wizard.kind.noCategory")
                : t("wizard.kind.categoryRule")}
          </FormHelperText>
        </FormControl>
      )}

      {showMasterEntry && (
        <Box sx={fullRow}>
          <Autocomplete<MaterialMasterEntry>
            size="small"
            options={view.availableMasterEntries}
            disabled={model.materialType === "Other" && !selectedCategory}
            getOptionLabel={(option) => `${option.code}: ${option.name}${option.grade ? ` (${option.grade})` : ""}`}
            isOptionEqualToValue={(option, val) => option.id === val.id}
            value={selectedMasterEntry}
            onChange={(_event, newValue) => set(masterEntryPatch(model, newValue, editing))}
            loading={lookups.masterEntriesLoading}
            renderInput={(params) => (
              <TextField
                {...params}
                size="small"
                name="materialMasterEntryId"
                required={!editing}
                error={Boolean(errors.materialMasterEntryId)}
                label={t(`wizard.kind.masterLabel.${masterEntryKey(model.materialType)}`)}
                placeholder={t("wizard.kind.selectMaster")}
                helperText={
                  model.materialMasterEntryId
                    ? t("wizard.kind.selectedMaster", { code: selectedMasterEntry?.code ?? model.code, unit: selectedMasterEntry?.baseUnit ?? model.unit })
                    : !editing
                      ? model.materialType === "Other"
                        ? selectedCategory
                          ? t("wizard.kind.otherMaster")
                          : t("wizard.kind.chooseCategory")
                        : t("wizard.kind.requiredMaster")
                      : t("wizard.kind.legacyMaster")
                }
              />
            )}
          />
        </Box>
      )}

      {model.materialType === "LyophilizedMicroorganism" && (
        <>
          <Box sx={fullRow}>
            <Autocomplete
              size="small"
              options={view.strainOptions}
              value={lookups.strains.find((s) => s.id === model.referenceOrganismId) ?? null}
              getOptionLabel={(s) => `${s.code} · ${s.name}`}
              isOptionEqualToValue={(o, v) => o.id === v.id}
              onChange={(_e, v) => set(strainPatch(v, editing))}
              renderInput={(params) => (
                <TextField
                  {...params}
                  name="referenceOrganismId"
                  label={t("wizard.kind.refOrganism")}
                  required={!editing}
                  error={Boolean(errors.referenceOrganismId)}
                  placeholder={t("wizard.kind.selectStrain")}
                  helperText={
                    editing && !model.referenceOrganismId
                      ? t("wizard.kind.legacyOrganism")
                      : t("wizard.kind.strainsManaged")
                  }
                />
              )}
            />
          </Box>
          {editing && !model.referenceOrganismId && (
            <>
              <TextField size="small" label={t("wizard.kind.organismStored")} value={editing.organism?.scientificName ?? "—"} disabled />
              <TextField size="small" label={t("wizard.kind.atccStored")} value={model.atccNumber || "—"} disabled />
            </>
          )}
        </>
      )}
    </>
  );
}
