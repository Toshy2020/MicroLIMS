import { Autocomplete, Box, FormControl, FormHelperText, InputLabel, MenuItem, Select, TextField } from "@mui/material";
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

function masterEntryLabel(type: MaterialFormModel["materialType"]): string {
  if (type === "Chemical") return "Reagent";
  if (type === "Indicator") return "Indicator";
  if (type === "PrimaryStandard") return "Primary Standard";
  if (type === "Other") return "Material";
  return "Reference Standard";
}

// What is being received: the configured media product, the reference strain, or the category and
// master entry that carry the material's name, code and rules.
export function MaterialKindFields({ model, set, errors, editing, lookups, view, activeSectionId }: KindFieldsProps) {
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
            helperText="Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration."
          />
        </Box>
      )}

      {model.materialType === "Other" && editing && editing.materialMasterEntryId && (
        <>
          <TextField size="small" label="Material category" value={editing.categoryName ?? "—"} disabled />
          <TextField
            size="small"
            label="Master entry"
            value={`${editing.materialMasterEntryCode ?? editing.code ?? ""}: ${editing.materialName}`}
            disabled
          />
        </>
      )}

      {model.materialType === "Other" && !editing && (
        <FormControl size="small" fullWidth required error={Boolean(errors.categoryId)} sx={fullRow}>
          <InputLabel id="material-category-label">Material category</InputLabel>
          <Select<number | "">
            labelId="material-category-label"
            label="Material category"
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
              ? "Choose the laboratory section first."
              : lookups.categories.length === 0 && !lookups.categoriesLoading
                ? "No category is available to this lab; a Section Head adds one under Master Testing Materials, Other Materials, Material categories."
                : "Sets the unit, storage and expiry rules for this lot."}
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
                label={`${masterEntryLabel(model.materialType)} Master Entry`}
                placeholder="Select master entry..."
                helperText={
                  model.materialMasterEntryId
                    ? `Selected: ${selectedMasterEntry?.code ?? model.code} (Base Unit: ${selectedMasterEntry?.baseUnit ?? model.unit})`
                    : !editing
                      ? model.materialType === "Other"
                        ? selectedCategory
                          ? "Required. The master entry sets the name and code; a missing material is added by its owner lab."
                          : "Choose the material category first."
                        : "Required. Master entry sets the name, code, and base unit."
                      : "Optional for legacy unlinked lots. Select an entry to link this lot."
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
                  label="Reference organism"
                  required={!editing}
                  error={Boolean(errors.referenceOrganismId)}
                  placeholder="Select strain"
                  helperText={
                    editing && !model.referenceOrganismId
                      ? "Received before reference organisms; pick a strain to link this lot (optional)."
                      : "Strains are managed under Organisms > Reference organisms."
                  }
                />
              )}
            />
          </Box>
          {editing && !model.referenceOrganismId && (
            <>
              <TextField size="small" label="Organism (stored)" value={editing.organism?.scientificName ?? "—"} disabled />
              <TextField size="small" label="ATCC No. (stored)" value={model.atccNumber || "—"} disabled />
            </>
          )}
        </>
      )}
    </>
  );
}
