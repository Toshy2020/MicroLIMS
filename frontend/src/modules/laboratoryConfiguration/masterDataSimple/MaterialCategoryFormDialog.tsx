import { useState, useEffect } from "react";
import {
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  FormHelperText,
  InputLabel,
  ListItemText,
  MenuItem,
  OutlinedInput,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { MATERIAL_UNITS } from "../../inventory/materials/types/materialTypes";
import type { MaterialUnit } from "../../inventory/materials/types/materialTypes";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import { MaterialCategory, MaterialCategoryService } from "./services/MaterialCategoryService";

interface MaterialCategoryFormDialogProps {
  open: boolean;
  // null = create a new category.
  category: MaterialCategory | null;
  sections: LaboratorySection[];
  onClose: () => void;
  onSuccess: (saved: MaterialCategory) => void;
}

// Creating or changing a category carries GMP rules (COA, release before use), so it always
// takes a reason and an electronic signature; the server enforces both.
export function MaterialCategoryFormDialog({ open, category, sections, onClose, onSuccess }: MaterialCategoryFormDialogProps) {
  const [name, setName] = useState("");
  const [codePrefix, setCodePrefix] = useState("");
  const [defaultUnit, setDefaultUnit] = useState<MaterialUnit>("Piece");
  const [storage, setStorage] = useState("");
  const [coaRequired, setCoaRequired] = useState(false);
  const [expiryRequired, setExpiryRequired] = useState(true);
  const [quantityTracked, setQuantityTracked] = useState(true);
  const [approvalBeforeUse, setApprovalBeforeUse] = useState(false);
  const [labelRequired, setLabelRequired] = useState(false);
  const [sectionIds, setSectionIds] = useState<number[]>([]);
  const [reason, setReason] = useState("");
  const [errors, setErrors] = useState<Partial<Record<"name" | "prefix", string>>>({});
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(category?.name ?? "");
    setCodePrefix(category?.codePrefix ?? "");
    setDefaultUnit(category?.defaultUnit ?? "Piece");
    setStorage(category?.defaultStorageCondition ?? "");
    setCoaRequired(category?.coaRequired ?? false);
    setExpiryRequired(category?.expiryRequired ?? true);
    setQuantityTracked(category?.quantityTracked ?? true);
    setApprovalBeforeUse(category?.approvalBeforeUse ?? false);
    setLabelRequired(category?.labelRequired ?? false);
    setSectionIds(category?.sectionIds ?? []);
    setReason("");
    setErrors({});
    setSignatureOpen(false);
  }, [open, category]);

  const trimmedName = name.trim();
  const trimmedPrefix = codePrefix.trim().toUpperCase();
  const trimmedReason = reason.trim();
  const prefixFrozen = Boolean(category?.hasEntries);

  const handleContinue = () => {
    const next: typeof errors = {};
    if (!trimmedName) next.name = "Name is required.";
    if (!/^[A-Z]{2,5}$/.test(trimmedPrefix)) next.prefix = "Use 2 to 5 capital letters.";
    setErrors(next);
    if (Object.keys(next).length === 0) setSignatureOpen(true);
  };

  const meaningStatement = category
    ? `I am changing the material category "${category.name}". The new rules apply to lots received afterwards. Reason: ${trimmedReason}`
    : `I am creating the material category "${trimmedName}" (${trimmedPrefix}). Reason: ${trimmedReason}`;

  const handleConfirmSignature = async (password: string) => {
    const req = {
      name: trimmedName,
      codePrefix: trimmedPrefix,
      defaultUnit,
      coaRequired,
      expiryRequired,
      quantityTracked,
      defaultStorageCondition: storage.trim() || null,
      approvalBeforeUse,
      labelRequired,
      // Empty selection = available to every section.
      sectionIds: sectionIds.length > 0 ? sectionIds : null,
      reason: trimmedReason,
      password
    };
    const saved = category ? await MaterialCategoryService.update(category.id, req) : await MaterialCategoryService.create(req);
    setSignatureOpen(false);
    onSuccess(saved);
    onClose();
  };

  const rule = (label: string, checked: boolean, onChange: (v: boolean) => void, hint: string) => (
    <Box>
      <FormControlLabel
        control={<Checkbox size="small" checked={checked} onChange={(e) => onChange(e.target.checked)} />}
        label={label}
      />
      <Typography variant="caption" color="text.secondary" sx={{ display: "block", ml: 4, mt: -0.5 }}>
        {hint}
      </Typography>
    </Box>
  );

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={category ? `Edit Category: ${category.name}` : "Add Material Category"}
        onClose={onClose}
        maxWidth="md"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={!trimmedName || !trimmedPrefix || !trimmedReason} onClick={handleContinue}>
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
            <TextField
              label="Name"
              required
              size="small"
              value={name}
              onChange={(e) => { setErrors((x) => ({ ...x, name: undefined })); setName(e.target.value); }}
              error={!!errors.name}
              helperText={errors.name ?? "e.g. Supplement, Filter Membrane, PPE"}
            />
            <TextField
              label="Code prefix"
              required
              size="small"
              value={codePrefix}
              onChange={(e) => { setErrors((x) => ({ ...x, prefix: undefined })); setCodePrefix(e.target.value.toUpperCase()); }}
              disabled={prefixFrozen}
              error={!!errors.prefix}
              helperText={
                errors.prefix ??
                (prefixFrozen
                  ? "This category has master entries; its prefix can no longer change."
                  : `Entry codes start with "${trimmedPrefix || "XXX"}-". Fixed once the category has entries.`)
              }
            />
            <FormControl size="small" required>
              <InputLabel id="category-unit-label">Default unit</InputLabel>
              <Select
                labelId="category-unit-label"
                label="Default unit"
                value={defaultUnit}
                onChange={(e) => setDefaultUnit(e.target.value as MaterialUnit)}
              >
                {MATERIAL_UNITS.map((u) => (
                  <MenuItem key={u} value={u}>{u}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Default storage condition"
              size="small"
              value={storage}
              onChange={(e) => setStorage(e.target.value)}
              placeholder="e.g. 2-8 °C, protect from light"
              helperText="Suggested at receipt; the receiver can change it."
            />
          </Box>

          <FormControl size="small">
            <InputLabel id="category-sections-label">Available in sections</InputLabel>
            <Select
              labelId="category-sections-label"
              label="Available in sections"
              multiple
              displayEmpty
              value={sectionIds}
              onChange={(e) => setSectionIds(e.target.value as number[])}
              input={<OutlinedInput label="Available in sections" />}
              renderValue={(selected) =>
                selected.length === 0
                  ? "All sections"
                  : selected.map((id) => sections.find((s) => s.sectionId === id)?.sectionName ?? `Section #${id}`).join(", ")
              }
            >
              {sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  <Checkbox size="small" checked={sectionIds.includes(s.sectionId)} />
                  <ListItemText primary={s.sectionName} />
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>Leave empty to offer the category to every section.</FormHelperText>
          </FormControl>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1 }}>
            {rule("COA required before use", coaRequired, setCoaRequired, "A current certificate of analysis must be on file.")}
            {rule("Expiry date required", expiryRequired, setExpiryRequired, "Receipt needs an expiry date.")}
            {rule("Quantity tracked", quantityTracked, setQuantityTracked, "Stock is decremented as it is used.")}
            {rule("Approval before use", approvalBeforeUse, setApprovalBeforeUse, "A second person releases each lot first.")}
            {rule("Label required", labelRequired, setLabelRequired, "Receipt prompts to print the stock label.")}
          </Box>

          <TextField
            label="Reason"
            required
            multiline
            rows={2}
            size="small"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Enter the justification for this change..."
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
