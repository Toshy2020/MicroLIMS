import { useEffect, useMemo, useState } from "react";
import {
  Button,
  Box,
  TextField,
  Select,
  MenuItem,
  ListSubheader,
  FormControl,
  InputLabel,
  Typography,
  Divider,
  Alert,
  Paper,
  useTheme,
  Autocomplete,
  createFilterOptions
} from "@mui/material";
import { OrganismPicker } from "../../../../components/OrganismPicker";
import { MediaProductPicker } from "../../../../components/MediaProductPicker";
import type { MediaProductOption } from "../../../../hooks/useMediaProducts";
import { MaterialService } from "../services/MaterialService";
import { EquipmentInventoryService } from "../../equipment/services/EquipmentInventoryService";
import { MaterialFormState, MaterialItem, MaterialType, MaterialUnit } from "../types/materialTypes";
import { MATERIAL_TYPE_OPTIONS } from "./MaterialFilterBar";
import { brandColors } from "../../../../theme";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { getMySections, LaboratorySection } from "../../../../services/laboratorySectionService";

interface MaterialTypeOption {
  label: string;
  value?: MaterialType;
  group: "Standard types" | "Types added by this lab";
  inputValue?: string;
}

const filterOptions = createFilterOptions<MaterialTypeOption>();

const MATERIAL_UNITS: MaterialUnit[] = [
  "Gram",
  "Kilogram",
  "Milliliter",
  "Liter",
  "Disc",
  "Vial",
  "Kit",
  "Piece",
  "Bottle",
  "Pack"
];

interface AddMaterialDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  editingItem: MaterialItem | null;
}

const INITIAL_FORM: MaterialFormState = {
  materialType: "DehydratedMedia",
  customType: "",
  materialName: "",
  manufacturerName: "",
  batchNumber: "",
  receivingDate: new Date().toISOString().slice(0, 10),
  expiryDate: "",
  code: "",
  location: "",
  quantityReceived: "",
  unit: "Gram",
  minimumStockLevel: "",
  purity: "",
  atccNumber: "",
  organismId: null,
  mediaProductId: null
};

export function AddMaterialDialog({ open, onClose, onSuccess, editingItem }: AddMaterialDialogProps) {
  const theme = useTheme();
  const [form, setForm] = useState<MaterialFormState>(INITIAL_FORM);
  const [typeOptionsData, setTypeOptionsData] = useState<{
    builtIn: MaterialType[];
    custom: string[];
  } | null>(null);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [selectedSectionId, setSelectedSectionId] = useState<number | "">("");
  const [equipmentList, setEquipmentList] = useState<any[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (editingItem) {
      setForm({
        materialType: editingItem.materialType,
        customType: editingItem.customType ?? "",
        materialName: editingItem.materialName,
        manufacturerName: editingItem.manufacturerName ?? "",
        batchNumber: editingItem.batchNumber,
        receivingDate: editingItem.receivingDate?.slice(0, 10) ?? "",
        expiryDate: editingItem.expiryDate?.slice(0, 10) ?? "",
        code: editingItem.code ?? "",
        location: editingItem.location,
        quantityReceived: editingItem.quantityReceived,
        unit: editingItem.unit,
        minimumStockLevel: editingItem.minimumStockLevel ?? "",
        purity: editingItem.purity != null ? editingItem.purity : "",
        atccNumber: editingItem.atccNumber ?? "",
        organismId: editingItem.organismId ?? null,
        mediaProductId: editingItem.mediaProductId ?? null
      });
      setSelectedSectionId("");
    } else {
      setForm({
        ...INITIAL_FORM,
        receivingDate: new Date().toISOString().slice(0, 10)
      });
      setSelectedSectionId("");
    }
    setError(null);

    if (open) {
      getMySections()
        .then((secs) => {
          setMySections(secs);
          if (!editingItem) {
            if (secs.length === 1) {
              setSelectedSectionId(secs[0].sectionId);
            } else {
              setSelectedSectionId("");
            }
          }
        })
        .catch(() => setMySections([]));

      setEquipmentLoading(true);
      EquipmentInventoryService.getAll()
        .then((data: any[]) => setEquipmentList(data || []))
        .catch(() => setEquipmentList([]))
        .finally(() => setEquipmentLoading(false));
    }
  }, [editingItem, open]);

  const activeSectionId = editingItem
    ? editingItem.sectionId
    : selectedSectionId !== ""
    ? Number(selectedSectionId)
    : undefined;

  const activeSection = mySections.find((s) => s.sectionId === activeSectionId);

  const roomStorageOption = activeSection
    ? activeSection.sectionCode === "MICRO"
      ? "Microbiology Lab"
      : activeSection.sectionCode === "FP"
      ? "Physicochemical Lab"
      : activeSection.sectionName
    : null;

  // Eligible storage equipment: Status = InService (filtered by active lab when known)
  const inServiceEquipment = equipmentList.filter(
    (e) =>
      (e.status === "InService" || e.status === 0 || e.status === "0") &&
      (activeSectionId != null ? e.sectionId === activeSectionId || e.sectionId == null : true)
  );

  const refrigerators = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("refrigerator")
  );

  const deepFreezers = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const freezers = inServiceEquipment.filter(
    (e) =>
      (e.instrumentType || "").toLowerCase().includes("freezer") &&
      !(e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const selectedEquipment = inServiceEquipment.find((eq) => {
    const fullTag = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
    return (
      form.location === fullTag ||
      form.location === eq.code ||
      (form.location && form.location.includes(eq.code))
    );
  });

  const isFallbackLocation = Boolean(
    form.location &&
      form.location !== roomStorageOption &&
      !inServiceEquipment.some(
        (eq) => `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})` === form.location
      )
  );
  const hasOtherOptions = Boolean(roomStorageOption || isFallbackLocation);

  const onMaterialTypeChange = async (type: MaterialType) => {
    try {
      const defaultUnit = await MaterialService.getDefaultUnit(type);
      setForm((f) => ({
        ...f,
        materialType: type,
        customType: "",
        unit: defaultUnit as MaterialUnit,
        mediaProductId: type === "DehydratedMedia" ? f.mediaProductId : null,
        purity: type === "ReferenceStandard" ? f.purity : ""
      }));
    } catch {
      setForm((f) => ({
        ...f,
        materialType: type,
        customType: "",
        mediaProductId: type === "DehydratedMedia" ? f.mediaProductId : null,
        purity: type === "ReferenceStandard" ? f.purity : ""
      }));
    }
  };

  useEffect(() => {
    if (!open) {
      setTypeOptionsData(null);
      return;
    }

    if (activeSectionId != null) {
      let canceled = false;
      MaterialService.getTypeOptions(activeSectionId)
        .then((data) => {
          if (canceled) return;
          setTypeOptionsData(data);
        })
        .catch(() => {
          if (!canceled) {
            setTypeOptionsData(null);
          }
        });

      return () => {
        canceled = true;
      };
    } else {
      setTypeOptionsData(null);
    }
  }, [open, activeSectionId, editingItem]);

  // When creating, a type the chosen lab doesn't offer (the default is
  // DehydratedMedia, which the Physicochemical lab doesn't have) switches to
  // the lab's first type.
  useEffect(() => {
    if (editingItem || !typeOptionsData || typeOptionsData.builtIn.length === 0) return;
    if (!typeOptionsData.builtIn.includes(form.materialType)) {
      void onMaterialTypeChange(typeOptionsData.builtIn[0]);
    }
  }, [typeOptionsData, editingItem, form.materialType]);

  const typeOptionsList: MaterialTypeOption[] = useMemo(() => {
    const list: MaterialTypeOption[] = [];

    // Built-in types: lab's builtIn if known, else all MATERIAL_TYPE_OPTIONS
    const builtInTypes: MaterialType[] = typeOptionsData
      ? [...typeOptionsData.builtIn]
      : MATERIAL_TYPE_OPTIONS.map((o) => o.value);

    // An existing item whose type is not in its lab's list must still display its current type
    if (
      editingItem &&
      editingItem.materialType !== "Other" &&
      !builtInTypes.includes(editingItem.materialType)
    ) {
      builtInTypes.push(editingItem.materialType);
    }

    for (const bt of builtInTypes) {
      const label = MATERIAL_TYPE_OPTIONS.find((o) => o.value === bt)?.label ?? bt;
      list.push({
        label,
        value: bt,
        group: "Standard types"
      });
    }

    // Lab custom types
    const customTypes: string[] = typeOptionsData ? [...typeOptionsData.custom] : [];

    // Existing item customType if not in list
    if (
      editingItem?.customType &&
      !customTypes.some((c) => c.toLowerCase() === editingItem.customType!.toLowerCase())
    ) {
      customTypes.push(editingItem.customType);
    }

    // Form current customType if not in list
    if (
      form.customType.trim() &&
      !customTypes.some((c) => c.toLowerCase() === form.customType.trim().toLowerCase()) &&
      !list.some((o) => o.label.toLowerCase() === form.customType.trim().toLowerCase())
    ) {
      customTypes.push(form.customType.trim());
    }

    for (const ct of customTypes) {
      list.push({
        label: ct,
        group: "Types added by this lab"
      });
    }

    return list;
  }, [typeOptionsData, editingItem, form.customType]);

  const selectedTypeOption = useMemo<MaterialTypeOption | null>(() => {
    if (form.materialType === "Other" && form.customType.trim()) {
      const trimmed = form.customType.trim();
      const existing = typeOptionsList.find(
        (o) => o.label.toLowerCase() === trimmed.toLowerCase()
      );
      return (
        existing ?? {
          label: trimmed,
          group: "Types added by this lab"
        }
      );
    }

    const matched = typeOptionsList.find((o) => o.value === form.materialType);
    if (matched) return matched;

    const label =
      MATERIAL_TYPE_OPTIONS.find((o) => o.value === form.materialType)?.label ??
      form.materialType;
    return {
      label,
      value: form.materialType,
      group: "Standard types"
    };
  }, [typeOptionsList, form.materialType, form.customType]);

  const applyTypeSelection = (val: MaterialTypeOption | string) => {
    let label = "";
    let explicitBuiltIn: MaterialType | undefined;

    if (typeof val === "string") {
      label = val.trim();
    } else if (val.inputValue) {
      label = val.inputValue.trim();
    } else {
      label = val.label.trim();
      explicitBuiltIn = val.value;
    }

    if (!label) return;

    // Check if the typed text case-insensitively equals a built-in label or value
    const matchedBuiltIn = MATERIAL_TYPE_OPTIONS.find(
      (o) =>
        o.label.toLowerCase() === label.toLowerCase() ||
        o.value.toLowerCase() === label.toLowerCase()
    );

    if (explicitBuiltIn || matchedBuiltIn) {
      const builtInType = explicitBuiltIn ?? matchedBuiltIn!.value;
      setForm((f) => ({
        ...f,
        materialType: builtInType,
        customType: ""
      }));
      void onMaterialTypeChange(builtInType);
    } else {
      setForm((f) => ({
        ...f,
        materialType: "Other",
        customType: label,
        mediaProductId: null,
        purity: ""
      }));
    }
  };

  const handleMediaProductChange = (id: number | null, product: MediaProductOption | null) => {
    if (!product || id === null) {
      if (editingItem && editingItem.mediaProductId === null) {
        setForm((f) => ({
          ...f,
          mediaProductId: null,
          materialName: editingItem.materialName,
          code: editingItem.code ?? ""
        }));
      } else {
        setForm((f) => ({
          ...f,
          mediaProductId: null,
          materialName: "",
          code: ""
        }));
      }
      return;
    }

    if (editingItem && editingItem.mediaProductId !== null && id === editingItem.mediaProductId) {
      setForm((f) => ({
        ...f,
        mediaProductId: id,
        materialName: editingItem.materialName,
        code: editingItem.code ?? ""
      }));
    } else {
      setForm((f) => ({
        ...f,
        mediaProductId: id,
        materialName: product.name,
        code: product.code
      }));
    }
  };

  const handleSave = async () => {
    setError(null);
    if (form.materialType === "DehydratedMedia" && !form.mediaProductId) {
      setError("Choose the configured media product for this dehydrated media.");
      return;
    }

    if (form.materialType === "ReferenceStandard") {
      if (form.purity === "" || form.purity == null) {
        setError("Purity percentage is required for reference standards.");
        return;
      }
      const p = Number(form.purity);
      if (isNaN(p) || p <= 0 || p > 100) {
        setError("Purity must be greater than 0 and less than or equal to 100.");
        return;
      }
    }

    if (
      !form.materialName.trim() ||
      !form.batchNumber.trim() ||
      !form.receivingDate ||
      !form.location.trim() ||
      form.quantityReceived === "" ||
      form.quantityReceived == null
    ) {
      setError("Material name, batch/lot number, receiving date, storage location, and quantity received are required.");
      return;
    }

    if (Number(form.quantityReceived) <= 0) {
      setError("Quantity received must be greater than zero.");
      return;
    }

    if (!editingItem && mySections.length > 1 && !selectedSectionId) {
      setError("Laboratory section is required.");
      return;
    }

    const payload = {
      materialType: form.materialType,
      customType:
        form.materialType === "Other" && form.customType.trim()
          ? form.customType.trim()
          : null,
      materialName: form.materialName.trim(),
      manufacturerName: form.manufacturerName.trim(),
      batchNumber: form.batchNumber.trim(),
      receivingDate: form.receivingDate,
      expiryDate: form.expiryDate || null,
      code: form.code.trim() || null,
      location: form.location.trim(),
      quantityReceived: Number(form.quantityReceived),
      unit: form.unit,
      minimumStockLevel: form.minimumStockLevel === "" ? null : Number(form.minimumStockLevel),
      atccNumber: form.materialType === "LyophilizedMicroorganism" ? form.atccNumber.trim() || null : null,
      organismId: form.materialType === "LyophilizedMicroorganism" ? form.organismId || null : null,
      mediaProductId: form.materialType === "DehydratedMedia" ? form.mediaProductId : null,
      purity: form.materialType === "ReferenceStandard" && form.purity !== "" ? Number(form.purity) : null,
      ...(!editingItem && mySections.length > 1 && selectedSectionId !== "" ? { sectionId: Number(selectedSectionId) } : {})
    };

    setSaving(true);
    try {
      if (editingItem) {
        await MaterialService.update(editingItem.id, payload);
        onSuccess("Material stock updated successfully.");
      } else {
        await MaterialService.create(payload);
        onSuccess("Material added to stock successfully.");
      }
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not save material stock.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1.5 }}
      title={
        <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
          {editingItem ? "Edit Material" : "Add Material to Stock"}
        </Typography>
      }
      actions={
        <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={saving}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 3,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {saving ? "Saving..." : editingItem ? "Save Changes" : "Add to Stock"}
          </Button>
        </Box>
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }}>
          {error}
        </Alert>
      )}

      {/* SECTION 1 — Material Information */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        1. Material Information
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 3 }}>
        <Autocomplete<MaterialTypeOption, false, false, true>
          freeSolo
          selectOnFocus
          handleHomeEndKeys
          options={typeOptionsList}
          groupBy={(option) => (typeof option === "string" ? "Types added by this lab" : option.group)}
          value={selectedTypeOption}
          onChange={(_event, newValue) => {
            if (!newValue) return;
            applyTypeSelection(newValue);
          }}
          filterOptions={(options, params) => {
            const filtered = filterOptions(options, params);
            const trimmed = params.inputValue.trim();
            const isExisting = options.some(
              (o) => o.label.toLowerCase() === trimmed.toLowerCase()
            );
            if (trimmed !== "" && !isExisting) {
              filtered.push({
                inputValue: trimmed,
                label: `Add "${trimmed}"`,
                group: "Types added by this lab"
              });
            }
            return filtered;
          }}
          getOptionLabel={(option) => {
            if (typeof option === "string") return option;
            if (option.inputValue) return option.inputValue;
            return option.label;
          }}
          isOptionEqualToValue={(option, val) => {
            if (typeof val === "string") {
              return option.label.toLowerCase() === val.toLowerCase();
            }
            if (val.value && option.value) {
              return option.value === val.value;
            }
            return option.label.toLowerCase() === val.label.toLowerCase();
          }}
          renderOption={(props, option) => {
            const { key, ...restProps } = props;
            const isAddOption = typeof option !== "string" && Boolean(option.inputValue);
            return (
              <li
                key={key}
                {...restProps}
                style={{
                  ...restProps.style,
                  ...(isAddOption ? { color: theme.palette.primary.main, fontWeight: 600 } : {})
                }}
              >
                {typeof option === "string" ? option : option.label}
              </li>
            );
          }}
          renderGroup={(params) => (
            <li key={params.key}>
              <ListSubheader
                component="div"
                sx={{
                  bgcolor: "background.paper",
                  fontWeight: 700,
                  fontSize: 11,
                  textTransform: "uppercase",
                  letterSpacing: 0.5,
                  color: "text.secondary",
                  lineHeight: "28px"
                }}
              >
                {params.group}
              </ListSubheader>
              <Box component="ul" sx={{ p: 0, m: 0 }}>
                {params.children}
              </Box>
            </li>
          )}
          sx={{ gridColumn: { xs: "1", sm: form.materialType === "DehydratedMedia" ? "span 2" : "1" } }}
          renderInput={(params) => (
            <TextField
              {...params}
              size="small"
              required
              label="Material Type"
              helperText="Pick a type or type a new one"
              onBlur={(e) => {
                const text = e.target.value?.trim();
                // Only a newly typed name - re-applying the current type would
                // reset a unit the user has changed.
                if (text && text.toLowerCase() !== selectedTypeOption?.label.toLowerCase()) {
                  applyTypeSelection(text);
                }
              }}
            />
          )}
        />

        {form.materialType === "DehydratedMedia" && (
          <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <MediaProductPicker
              value={form.mediaProductId}
              onChange={handleMediaProductChange}
              required
              helperText="Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration."
            />
          </Box>
        )}

        {!editingItem && mySections.length > 1 && (
          <FormControl size="small" fullWidth required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="dialog-section-label">Laboratory Section</InputLabel>
            <Select<number | "">
              labelId="dialog-section-label"
              label="Laboratory Section"
              value={selectedSectionId}
              onChange={(e) => setSelectedSectionId(e.target.value === "" ? "" : Number(e.target.value))}
            >
              <MenuItem value="">
                <em>Select Laboratory Section...</em>
              </MenuItem>
              {mySections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName} ({s.departmentName})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        <TextField
          size="small"
          required
          label="Material Name"
          placeholder="e.g. Tryptic Soy Agar Powder"
          value={form.materialName}
          onChange={(e) => setForm({ ...form, materialName: e.target.value })}
          disabled={form.materialType === "DehydratedMedia"}
        />

        <TextField
          size="small"
          label="Manufacturer"
          placeholder="e.g. Oxoid, Merck, Difco"
          value={form.manufacturerName}
          onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })}
        />

        <TextField
          size="small"
          label={form.materialType === "DehydratedMedia" ? "Media code" : "Code / Catalog No."}
          placeholder={form.materialType === "DehydratedMedia" ? "e.g. TSA" : "e.g. CM0131B"}
          value={form.code}
          onChange={(e) => setForm({ ...form, code: e.target.value })}
          disabled={form.materialType === "DehydratedMedia"}
        />

        {form.materialType === "LyophilizedMicroorganism" && (
          <>
            <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
              <OrganismPicker
                value={form.organismId}
                onChange={(id) => setForm({ ...form, organismId: id })}
              />
            </Box>
            <TextField
              size="small"
              label="ATCC No."
              placeholder="e.g. ATCC 6538"
              value={form.atccNumber}
              onChange={(e) => setForm({ ...form, atccNumber: e.target.value })}
            />
          </>
        )}

        {form.materialType === "ReferenceStandard" && (
          <TextField
            size="small"
            required
            label="Purity (%)"
            placeholder="e.g. 99.8"
            type="number"
            value={form.purity}
            onChange={(e) => setForm({ ...form, purity: e.target.value })}
            slotProps={{
              htmlInput: { step: "0.001", min: "0.001", max: "100" }
            }}
            helperText="Purity percentage (0 < p ≤ 100)"
          />
        )}
      </Box>

      <Divider sx={{ my: 2.5 }} />

      {/* SECTION 2 — Batch & Quantity */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        2. Batch &amp; Quantity
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2, mb: 1.5 }}>
        <TextField
          size="small"
          required
          label="Batch / Lot No."
          placeholder="e.g. 3458921"
          value={form.batchNumber}
          onChange={(e) => setForm({ ...form, batchNumber: e.target.value })}
        />

        <FormControl size="small" fullWidth required sx={{ gridColumn: { sm: "span 2", md: "span 2" } }}>
          <InputLabel id="storage-location-label">Storage Location</InputLabel>
          <Select
            labelId="storage-location-label"
            label="Storage Location"
            value={form.location}
            onChange={(e) => setForm({ ...form, location: e.target.value })}
            disabled={equipmentLoading}
          >
            <MenuItem value="">
              <em>{equipmentLoading ? "Loading storage locations..." : "Select storage location..."}</em>
            </MenuItem>

            <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment — Refrigerator
            </ListSubheader>
            {refrigerators.length > 0 ? (
              refrigerators.map((eq) => {
                const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-ref" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service refrigerator configured
              </MenuItem>
            )}

            <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment — Deep Freezer
            </ListSubheader>
            {deepFreezers.length > 0 ? (
              deepFreezers.map((eq) => {
                const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-df" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service deep freezer configured
              </MenuItem>
            )}

            <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment — Freezer
            </ListSubheader>
            {freezers.length > 0 ? (
              freezers.map((eq) => {
                const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-fr" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service freezer configured
              </MenuItem>
            )}

            {hasOtherOptions && (
              <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Other
              </ListSubheader>
            )}
            {roomStorageOption && (
              <MenuItem value={roomStorageOption}>
                {roomStorageOption}
              </MenuItem>
            )}

            {/* Fallback for legacy material locations when editing */}
            {isFallbackLocation && (
              <MenuItem value={form.location}>
                {form.location} (Current Location)
              </MenuItem>
            )}
          </Select>
        </FormControl>

        <TextField
          size="small"
          required
          type="number"
          label="Quantity Received"
          value={form.quantityReceived}
          onChange={(e) => setForm({ ...form, quantityReceived: e.target.value })}
          slotProps={{
            htmlInput: { min: 0, step: "any" }
          }}
        />

        <FormControl size="small" fullWidth required>
          <InputLabel id="dialog-unit-label">Unit</InputLabel>
          <Select
            labelId="dialog-unit-label"
            label="Unit"
            value={form.unit}
            onChange={(e) => setForm({ ...form, unit: e.target.value as MaterialUnit })}
          >
            {MATERIAL_UNITS.map((u) => (
              <MenuItem key={u} value={u}>
                {u}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          type="number"
          label="Min. Stock Level (optional)"
          placeholder="e.g. 500"
          value={form.minimumStockLevel}
          onChange={(e) => setForm({ ...form, minimumStockLevel: e.target.value })}
          slotProps={{
            htmlInput: { min: 0, step: "any" }
          }}
        />
      </Box>

      {/* Selected Equipment Read-Only Details Card */}
      {selectedEquipment && (
        <Paper
          variant="outlined"
          sx={{
            mb: 3,
            p: 1.5,
            bgcolor: "background.default",
            borderColor: "divider",
            borderRadius: 1
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, 1fr)",
                md: "repeat(4, 1fr)"
              },
              gap: 1.5
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Selected Storage
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                {selectedEquipment.instrumentType} — {selectedEquipment.manufacturerName || "Asset"}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Code
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {selectedEquipment.code}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Location
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {selectedEquipment.location || "—"}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Status
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.main" }}>
                In Service
              </Typography>
            </Box>
          </Box>
        </Paper>
      )}

      {/* Room Storage Read-Only Details Card */}
      {roomStorageOption && form.location === roomStorageOption && (
        <Paper
          variant="outlined"
          sx={{
            mb: 3,
            p: 1.5,
            bgcolor: "background.default",
            borderColor: "divider",
            borderRadius: 1
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, 1fr)",
                md: "repeat(3, 1fr)"
              },
              gap: 1.5
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Selected Storage
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                {roomStorageOption}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Storage Type
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                Ambient / Room Storage
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Location
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {activeSection?.sectionName ?? roomStorageOption}
              </Typography>
            </Box>
          </Box>
        </Paper>
      )}

      <Divider sx={{ my: 2.5 }} />

      {/* SECTION 3 — Dates */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        3. Dates & Traceability
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <TextField
          size="small"
          required
          type="date"
          label="Receiving Date"
          value={form.receivingDate}
          onChange={(e) => setForm({ ...form, receivingDate: e.target.value })}
          slotProps={{
            inputLabel: { shrink: true }
          }}
        />

        <TextField
          size="small"
          type="date"
          label="Expiry Date"
          value={form.expiryDate}
          onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
          slotProps={{
            inputLabel: { shrink: true }
          }}
        />
      </Box>

      {editingItem && (
        <Box sx={{ mt: 2.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <strong>Note:</strong> Changing Quantity Received adjusts Quantity Remaining by the difference (a receiving correction) —
            it preserves consumption already recorded by Media Preparation or Cryovials.
          </Typography>
        </Box>
      )}
    </FloatingDialog>
  );
}
