import { useState, useEffect, useRef } from "react";
import {
  TextField,
  Select,
  MenuItem,
  Stack,
  Box,
  Typography,
  Autocomplete,
  AutocompleteInputChangeReason,
  Checkbox,
  FormControlLabel,
} from "@mui/material";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { FieldHint, hintDescribedBy } from "../../../../components/FieldHint";
import { toast } from "sonner";
import { useTranslation } from "../../../../i18n";
import { RecordChangedBanner } from "../../../../components/RecordChangedBanner";
import { getItemHints } from "../hints";
import { CopyItemOptions, Item, ItemService } from "../services/ItemService";
import { FormDialog } from "../../../../components/lab";
import { documentControlService } from "../../../documentControl/services/documentControlService";
import type { DocumentMasterSummaryDto } from "../../../documentControl/types/documentControlTypes";

interface AddItemDialogProps {
  open: boolean;
  itemToEdit: Item | null;
  existingItems?: Item[];
  startFromExisting?: boolean;
  onClose: () => void;
  // Called with the freshly loaded item when the user reloads from the "changed by someone else"
  // banner, so the caller's copy (and the row version its save sends) is replaced.
  onItemReloaded?: (item: Item) => void;
  onSave: (itemData: {
    name: string;
    code: string;
    category: string;
    sopNumber: string;
    testCodes: string[];
    minSampleQuantity?: number | null;
    minRetestSampleQuantity?: number | null;
    minSampleQuantityUnit?: string | null;
    copyFrom?: CopyItemOptions | null;
  }) => Promise<void>;
}

const CATEGORY_VALUES = ["FinishedProduct", "RawMaterial", "PackagingMaterial"] as const;

export function AddItemDialog({
  open,
  itemToEdit,
  existingItems,
  startFromExisting = false,
  onClose,
  onItemReloaded,
  onSave,
}: AddItemDialogProps) {
  const { t } = useTranslation("labConfig");
  const hints = getItemHints(t);
  const categories = CATEGORY_VALUES.map((value) => ({ value, label: t(`items.categories.${value}`) }));
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [sopNumber, setSopNumber] = useState("");
  const [category, setCategory] = useState("FinishedProduct");
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [minSampleQuantity, setMinSampleQuantity] = useState<number | null>(null);
  const [minRetestSampleQuantity, setMinRetestSampleQuantity] = useState<number | null>(null);
  const [minSampleQuantityUnit, setMinSampleQuantityUnit] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [sourceItem, setSourceItem] = useState<Item | null>(null);
  const [copySpecifications, setCopySpecifications] = useState(true);
  const [copyAutoAssignedTests, setCopyAutoAssignedTests] = useState(true);
  const [fetchedItems, setFetchedItems] = useState<Item[] | null>(null);
  const [loadingSourceItems, setLoadingSourceItems] = useState(false);

  const [options, setOptions] = useState<DocumentMasterSummaryDto[]>([]);
  const [searchingDocs, setSearchingDocs] = useState(false);
  const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (itemToEdit) {
      setName(itemToEdit.name);
      setCode(itemToEdit.code);
      setSopNumber(itemToEdit.sopNumber || "");
      setCategory(itemToEdit.category);
      setTestCodes(itemToEdit.assignedTests?.map((t) => t.testCode) || []);
      setMinSampleQuantity(itemToEdit.minSampleQuantity ?? null);
      setMinRetestSampleQuantity(itemToEdit.minRetestSampleQuantity ?? null);
      setMinSampleQuantityUnit(itemToEdit.minSampleQuantityUnit ?? "");
    } else {
      setName("");
      setCode("");
      setSopNumber("");
      setCategory("FinishedProduct");
      setTestCodes([]);
      setMinSampleQuantity(null);
      setMinRetestSampleQuantity(null);
      setMinSampleQuantityUnit("");
    }
    setSourceItem(null);
    setCopySpecifications(true);
    setCopyAutoAssignedTests(true);
    setOptions([]);
    setSearchingDocs(false);
    setError(null);
  }, [itemToEdit, open]);

  useEffect(() => {
    if (!open || itemToEdit || existingItems !== undefined || !startFromExisting) return;
    let cancelled = false;
    setLoadingSourceItems(true);
    ItemService.getAll()
      .then((all) => {
        if (!cancelled) setFetchedItems(all);
      })
      .catch(() => {
        if (!cancelled) setFetchedItems([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingSourceItems(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, itemToEdit, existingItems, startFromExisting]);

  const ensureSourceItemsLoaded = () => {
    if (existingItems !== undefined || fetchedItems !== null || loadingSourceItems) return;
    setLoadingSourceItems(true);
    ItemService.getAll()
      .then((all) => setFetchedItems(all))
      .catch(() => setFetchedItems([]))
      .finally(() => setLoadingSourceItems(false));
  };

  useEffect(() => {
    return () => {
      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current);
      }
    };
  }, []);

  const handlePickSource = (picked: Item | null) => {
    setSourceItem(picked);
    if (!picked) return;
    // Code, name, SOP number and documents are never copied from the source item.
    if (categories.some((c) => c.value === picked.category)) {
      setCategory(picked.category);
    }
    setMinSampleQuantity(picked.minSampleQuantity ?? null);
    setMinRetestSampleQuantity(picked.minRetestSampleQuantity ?? null);
    setMinSampleQuantityUnit(picked.minSampleQuantityUnit ?? "");
  };

  const handleSopInputChange = (
    _event: React.SyntheticEvent,
    newInputValue: string,
    reason: AutocompleteInputChangeReason
  ) => {
    if (reason === "input") {
      setSopNumber(newInputValue);

      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current);
      }

      const trimmed = newInputValue.trim();
      if (trimmed.length < 2) {
        setOptions([]);
        setSearchingDocs(false);
        return;
      }

      setSearchingDocs(true);
      searchTimeoutRef.current = setTimeout(async () => {
        try {
          const res = await documentControlService.getLibrary({
            searchTerm: trimmed,
            pageSize: 20,
          });
          setOptions(res?.items || []);
        } catch {
          setOptions([]);
        } finally {
          setSearchingDocs(false);
        }
      }, 300);
    } else if (reason === "clear") {
      setSopNumber("");
      setOptions([]);
      setSearchingDocs(false);
      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current);
      }
    }
  };

  const handleSopChange = (
    _event: React.SyntheticEvent,
    newValue: string | DocumentMasterSummaryDto | null
  ) => {
    if (searchTimeoutRef.current) {
      clearTimeout(searchTimeoutRef.current);
    }
    setSearchingDocs(false);
    if (!newValue) {
      setSopNumber("");
    } else if (typeof newValue === "string") {
      setSopNumber(newValue);
    } else {
      setSopNumber(newValue.companyDocumentCode);
    }
  };

  const handleSubmit = async () => {
    setError(null);
    if (!itemToEdit && startFromExisting && !sourceItem) {
      setError(t("items.addDialog.errors.selectSourceItem"));
      return;
    }
    const copiesTestsFromSource = Boolean(!itemToEdit && sourceItem && copyAutoAssignedTests);
    if (!name.trim() || !code.trim() || (testCodes.length === 0 && !copiesTestsFromSource)) {
      setError(t("items.addDialog.errors.requiredFields"));
      return;
    }

    setSaving(true);
    try {
      const payload: {
        name: string;
        code: string;
        category: string;
        sopNumber: string;
        testCodes: string[];
        minSampleQuantity?: number | null;
        minRetestSampleQuantity?: number | null;
        minSampleQuantityUnit?: string | null;
        copyFrom?: CopyItemOptions | null;
      } = {
        name: name.trim(),
        code: code.trim(),
        category,
        sopNumber: sopNumber.trim(),
        testCodes,
      };
      if (category === "RawMaterial") {
        if (minSampleQuantity !== null) payload.minSampleQuantity = minSampleQuantity;
        if (minRetestSampleQuantity !== null) payload.minRetestSampleQuantity = minRetestSampleQuantity;
        if (minSampleQuantityUnit.trim()) payload.minSampleQuantityUnit = minSampleQuantityUnit.trim();
      }
      if (!itemToEdit && sourceItem) {
        payload.copyFrom = {
          sourceItemId: sourceItem.id,
          specifications: copySpecifications,
          autoAssignedTests: copyAutoAssignedTests,
        };
      }
      await onSave(payload);
      onClose();
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
          : err instanceof Error
          ? err.message
          : t("items.addDialog.errors.saveFailed");
      setError(msg || t("items.addDialog.errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const editFormIsDirty = itemToEdit
    ? JSON.stringify([name, code, sopNumber, category, testCodes, minSampleQuantity, minRetestSampleQuantity, minSampleQuantityUnit]) !==
      JSON.stringify([
        itemToEdit.name,
        itemToEdit.code,
        itemToEdit.sopNumber || "",
        itemToEdit.category,
        itemToEdit.assignedTests?.map((t) => t.testCode) || [],
        itemToEdit.minSampleQuantity ?? null,
        itemToEdit.minRetestSampleQuantity ?? null,
        itemToEdit.minSampleQuantityUnit ?? "",
      ])
    : false;

  const reloadItem = async () => {
    if (!itemToEdit) return;
    try {
      const fresh = (await ItemService.getAll()).find((i) => i.id === itemToEdit.id);
      if (!fresh) {
        toast.error(t("items.addDialog.errors.reloadFailed"));
        return;
      }
      onItemReloaded?.(fresh);
    } catch {
      toast.error(t("items.addDialog.errors.reloadFailed"));
    }
  };

  const sourceOptions = existingItems ?? fetchedItems ?? [];
  const showCopySection = !itemToEdit && (startFromExisting || sourceItem !== null);

  return (
    // A form, so Enter in a text field saves the item.
    <FormDialog
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      maxWidth="sm"
      title={
        itemToEdit
          ? t("items.addDialog.editTitle", { name: itemToEdit.name })
          : startFromExisting
          ? t("items.addDialog.newFromExistingTitle")
          : t("items.addDialog.addTitle")
      }
      submitLabel={itemToEdit ? t("items.addDialog.saveChanges") : t("items.addDialog.saveItem")}
      submitting={saving}
      error={error}
    >
      <Stack spacing={2}>
        {open && itemToEdit && (
          <RecordChangedBanner type="item" id={itemToEdit.id} dirty={editFormIsDirty} onReload={() => void reloadItem()} />
        )}
        {!itemToEdit && (
          <Box sx={{ p: showCopySection ? 1.5 : 0, border: showCopySection ? "1px solid" : "none", borderColor: "divider", borderRadius: 1 }}>
            <Autocomplete
              size="small"
              options={sourceOptions}
              value={sourceItem}
              loading={loadingSourceItems}
              onOpen={ensureSourceItemsLoaded}
              onChange={(_, value) => handlePickSource(value)}
              getOptionLabel={(o) => `${o.name} (${o.code})`}
              isOptionEqualToValue={(a, b) => a.id === b.id}
              renderInput={(params) => (
                <TextField
                  {...params}
                  size="small"
                  label={t("items.addDialog.copyFromLabel")}
                  placeholder={t("items.addDialog.copyFromPlaceholder")}
                />
              )}
            />
            {showCopySection && (
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mt: 1 }}>
                <FormControlLabel
                  control={
                    <Checkbox
                      size="small"
                      checked={copySpecifications}
                      onChange={(e) => setCopySpecifications(e.target.checked)}
                    />
                  }
                  label={t("items.addDialog.copySpecifications")}
                />
                <FormControlLabel
                  control={
                    <Checkbox
                      size="small"
                      checked={copyAutoAssignedTests}
                      onChange={(e) => setCopyAutoAssignedTests(e.target.checked)}
                    />
                  }
                  label={t("items.addDialog.copyAutoAssignedTests")}
                />
              </Stack>
            )}
            {showCopySection && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                {t("items.addDialog.copyNote")}
              </Typography>
            )}
          </Box>
        )}

        <TextField
          size="small"
          label={t("items.addDialog.itemNameLabel")}
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          fullWidth
        />

        <Stack direction="row" spacing={2}>
          <TextField
            size="small"
            label={t("items.addDialog.itemCodeLabel")}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
            sx={{ flex: 1 }}
            slotProps={{ input: { "aria-describedby": hintDescribedBy("items-item-code") } }}
          />
          <FieldHint id="items-item-code" label={t("items.addDialog.itemCodeHintLabel")} hint={hints.itemCode} />
          <Autocomplete<DocumentMasterSummaryDto, false, false, true>
            freeSolo
            size="small"
            options={options}
            loading={searchingDocs}
            value={sopNumber}
            onChange={handleSopChange}
            onInputChange={handleSopInputChange}
            filterOptions={(x) => x}
            getOptionLabel={(option) => {
              if (typeof option === "string") return option;
              return `${option.companyDocumentCode} — ${option.title}`;
            }}
            isOptionEqualToValue={(option, val) => {
              if (typeof val === "string") {
                return option.companyDocumentCode === val;
              }
              return option.companyDocumentCode === val?.companyDocumentCode;
            }}
            renderOption={(props, option) => {
              const { key, ...restProps } = props;
              const label =
                typeof option === "string"
                  ? option
                  : `${option.companyDocumentCode} — ${option.title}`;
              return (
                <li key={key ?? (typeof option === "string" ? option : option.id)} {...restProps}>
                  {label}
                </li>
              );
            }}
            renderInput={(params) => (
              <TextField
                {...params}
                size="small"
                label={t("items.addDialog.sopNumberLabel")}
              />
            )}
            sx={{ flex: 1 }}
          />
          <FieldHint id="items-sop-number" label={t("items.addDialog.sopNumberHintLabel")} hint={hints.sopNumber} />
        </Stack>

        <Box>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", mb: 0.5 }}>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block" }}>
              {t("items.addDialog.categoryLabel")}
            </Typography>
            <FieldHint id="items-category" label={t("items.addDialog.categoryLabel")} hint={hints.category} />
          </Stack>
          <Select
            size="small"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            fullWidth
            inputProps={{ "aria-label": t("items.addDialog.categoryLabel"), "aria-describedby": hintDescribedBy("items-category") }}
          >
            {categories.map((c) => (
              <MenuItem key={c.value} value={c.value}>
                {c.label}
              </MenuItem>
            ))}
          </Select>
        </Box>

        {category === "RawMaterial" && (
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 1.5 }}>
            <TextField
              label={t("items.addDialog.minSampleQtyLabel")}
              size="small"
              type="number"
              value={minSampleQuantity ?? ""}
              onChange={(e) => setMinSampleQuantity(e.target.value === "" ? null : Number(e.target.value))}
              slotProps={{ htmlInput: { step: "any" } }}
            />
            <TextField
              label={t("items.addDialog.minRetestQtyLabel")}
              size="small"
              type="number"
              value={minRetestSampleQuantity ?? ""}
              onChange={(e) => setMinRetestSampleQuantity(e.target.value === "" ? null : Number(e.target.value))}
              slotProps={{ htmlInput: { step: "any" } }}
            />
            <TextField
              label={t("items.addDialog.quantityUnitLabel")}
              size="small"
              placeholder={t("items.addDialog.quantityUnitPlaceholder")}
              value={minSampleQuantityUnit}
              onChange={(e) => setMinSampleQuantityUnit(e.target.value)}
            />
          </Box>
        )}

        <Box>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", mb: 0.5 }}>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block" }}>
              {t("items.addDialog.assignedTestsHeading")}
            </Typography>
            <FieldHint id="items-assigned-tests" label={t("items.addDialog.assignedTestsLabel")} hint={hints.assignedTests} />
          </Stack>
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label={t("items.addDialog.assignedTestsLabel")} />
          {!itemToEdit && sourceItem && copyAutoAssignedTests && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
              {sourceItem.assignedTests?.length
                ? t("items.addDialog.copyTestsWithCodes", {
                    name: sourceItem.name,
                    codes: sourceItem.assignedTests.map((x) => x.testCode).join(", "),
                  })
                : t("items.addDialog.copyTestsNoCodes", { name: sourceItem.name })}
            </Typography>
          )}
        </Box>
      </Stack>
    </FormDialog>
  );
}
