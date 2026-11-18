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
import { i18n, useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("masterData");
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
    if (!trimmedName) next.name = t("materialCategoryForm.nameRequired");
    if (!/^[A-Z]{2,5}$/.test(trimmedPrefix)) next.prefix = t("materialCategoryForm.prefixInvalid");
    setErrors(next);
    if (Object.keys(next).length === 0) setSignatureOpen(true);
  };

  const enT = i18n.getFixedT("en", "masterData");
  const meaningStatement = category
    ? enT("materialCategoryForm.meaningStatement_update", { name: category.name, reason: trimmedReason })
    : enT("materialCategoryForm.meaningStatement_create", { name: trimmedName, prefix: trimmedPrefix, reason: trimmedReason });
  const meaningStatementTranslation = category
    ? t("materialCategoryForm.meaningStatement_update", { name: category.name, reason: trimmedReason })
    : t("materialCategoryForm.meaningStatement_create", { name: trimmedName, prefix: trimmedPrefix, reason: trimmedReason });

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
        title={category ? t("materialCategoryForm.editTitle", { name: category.name }) : t("materialCategoryForm.addTitle")}
        onClose={onClose}
        maxWidth="md"
        actions={
          <>
            <Button onClick={onClose}>{t("common.cancel")}</Button>
            <Button variant="contained" disabled={!trimmedName || !trimmedPrefix || !trimmedReason} onClick={handleContinue}>
              {t("materialCategoryForm.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
            <TextField
              label={t("materialCategoryForm.nameLabel")}
              required
              size="small"
              value={name}
              onChange={(e) => { setErrors((x) => ({ ...x, name: undefined })); setName(e.target.value); }}
              error={!!errors.name}
              helperText={errors.name ?? t("materialCategoryForm.nameHelper")}
            />
            <TextField
              label={t("materialCategoryForm.prefixLabel")}
              required
              size="small"
              value={codePrefix}
              onChange={(e) => { setErrors((x) => ({ ...x, prefix: undefined })); setCodePrefix(e.target.value.toUpperCase()); }}
              disabled={prefixFrozen}
              error={!!errors.prefix}
              helperText={
                errors.prefix ??
                (prefixFrozen
                  ? t("materialCategoryForm.prefixFrozenHelper")
                  : t("materialCategoryForm.prefixHelper", { prefix: trimmedPrefix || "XXX" }))
              }
            />
            <FormControl size="small" required>
              <InputLabel id="category-unit-label">{t("materialCategoryForm.defaultUnitLabel")}</InputLabel>
              <Select
                labelId="category-unit-label"
                label={t("materialCategoryForm.defaultUnitLabel")}
                value={defaultUnit}
                onChange={(e) => setDefaultUnit(e.target.value as MaterialUnit)}
              >
                {MATERIAL_UNITS.map((u) => (
                  <MenuItem key={u} value={u}>{u}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label={t("materialCategoryForm.defaultStorageLabel")}
              size="small"
              value={storage}
              onChange={(e) => setStorage(e.target.value)}
              placeholder={t("materialCategoryForm.defaultStoragePlaceholder")}
              helperText={t("materialCategoryForm.defaultStorageHelper")}
            />
          </Box>

          <FormControl size="small">
            <InputLabel id="category-sections-label">{t("materialCategoryForm.sectionsLabel")}</InputLabel>
            <Select
              labelId="category-sections-label"
              label={t("materialCategoryForm.sectionsLabel")}
              multiple
              displayEmpty
              value={sectionIds}
              onChange={(e) => setSectionIds(e.target.value as number[])}
              input={<OutlinedInput label={t("materialCategoryForm.sectionsLabel")} />}
              renderValue={(selected) =>
                selected.length === 0
                  ? t("materialCategories.allSections")
                  : selected.map((id) => sections.find((s) => s.sectionId === id)?.sectionName ?? t("materialCategories.sectionFallback", { id })).join(", ")
              }
            >
              {sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  <Checkbox size="small" checked={sectionIds.includes(s.sectionId)} />
                  <ListItemText primary={s.sectionName} />
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>{t("materialCategoryForm.sectionsHelper")}</FormHelperText>
          </FormControl>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1 }}>
            {rule(t("materialCategoryForm.rules.coaLabel"), coaRequired, setCoaRequired, t("materialCategoryForm.rules.coaHint"))}
            {rule(t("materialCategoryForm.rules.expiryLabel"), expiryRequired, setExpiryRequired, t("materialCategoryForm.rules.expiryHint"))}
            {rule(t("materialCategoryForm.rules.quantityLabel"), quantityTracked, setQuantityTracked, t("materialCategoryForm.rules.quantityHint"))}
            {rule(t("materialCategoryForm.rules.approvalLabel"), approvalBeforeUse, setApprovalBeforeUse, t("materialCategoryForm.rules.approvalHint"))}
            {rule(t("materialCategoryForm.rules.labelLabel"), labelRequired, setLabelRequired, t("materialCategoryForm.rules.labelHint"))}
          </Box>

          <TextField
            label={t("materialCategoryForm.reasonLabel")}
            required
            multiline
            rows={2}
            size="small"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("materialCategoryForm.reasonPlaceholder")}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </>
  );
}
