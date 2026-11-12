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
} from "@mui/material";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { Item } from "../services/ItemService";
import { FormDialog } from "../../../../components/lab";
import { documentControlService } from "../../../documentControl/services/documentControlService";
import type { DocumentMasterSummaryDto } from "../../../documentControl/types/documentControlTypes";

interface AddItemDialogProps {
  open: boolean;
  itemToEdit: Item | null;
  onClose: () => void;
  onSave: (itemData: {
    name: string;
    code: string;
    category: string;
    sopNumber: string;
    testCodes: string[];
    minSampleQuantity?: number | null;
    minRetestSampleQuantity?: number | null;
    minSampleQuantityUnit?: string | null;
  }) => Promise<void>;
}

const CATEGORIES = [
  { value: "FinishedProduct", label: "Product" },
  { value: "RawMaterial", label: "Raw Material" },
  { value: "PackagingMaterial", label: "Packaging Material" },
];

export function AddItemDialog({ open, itemToEdit, onClose, onSave }: AddItemDialogProps) {
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
    setOptions([]);
    setSearchingDocs(false);
    setError(null);
  }, [itemToEdit, open]);

  useEffect(() => {
    return () => {
      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current);
      }
    };
  }, []);

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
    if (!name.trim() || !code.trim() || testCodes.length === 0) {
      setError("Item Name, Item Code, and at least one assigned test are required.");
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
      await onSave(payload);
      onClose();
    } catch (err: unknown) {
      const msg =
        err && typeof err === "object" && "response" in err
          ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
          : err instanceof Error
          ? err.message
          : "Failed to save item.";
      setError(msg || "Failed to save item.");
    } finally {
      setSaving(false);
    }
  };

  return (
    // A form, so Enter in a text field saves the item.
    <FormDialog
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      maxWidth="sm"
      title={itemToEdit ? `Edit Item: ${itemToEdit.name}` : "Add New Item"}
      submitLabel={itemToEdit ? "Save Changes" : "Save Item"}
      submitting={saving}
      error={error}
    >
      <Stack spacing={2}>
        <TextField
          size="small"
          label="Item Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          fullWidth
        />

        <Stack direction="row" spacing={2}>
          <TextField
            size="small"
            label="Item Code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
            sx={{ flex: 1 }}
          />
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
                label="SOP Number"
              />
            )}
            sx={{ flex: 1 }}
          />
        </Stack>

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
            Category
          </Typography>
          <Select size="small" value={category} onChange={(e) => setCategory(e.target.value)} fullWidth inputProps={{ "aria-label": "Category" }}>
            {CATEGORIES.map((c) => (
              <MenuItem key={c.value} value={c.value}>
                {c.label}
              </MenuItem>
            ))}
          </Select>
        </Box>

        {category === "RawMaterial" && (
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 1.5 }}>
            <TextField
              label="Min Sample Qty"
              size="small"
              type="number"
              value={minSampleQuantity ?? ""}
              onChange={(e) => setMinSampleQuantity(e.target.value === "" ? null : Number(e.target.value))}
              slotProps={{ htmlInput: { step: "any" } }}
            />
            <TextField
              label="Min Retest Qty"
              size="small"
              type="number"
              value={minRetestSampleQuantity ?? ""}
              onChange={(e) => setMinRetestSampleQuantity(e.target.value === "" ? null : Number(e.target.value))}
              slotProps={{ htmlInput: { step: "any" } }}
            />
            <TextField
              label="Quantity Unit"
              size="small"
              placeholder="e.g. g, mL"
              value={minSampleQuantityUnit}
              onChange={(e) => setMinSampleQuantityUnit(e.target.value)}
            />
          </Box>
        )}

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
            Assigned Tests (Auto-assigned on sample receipt)
          </Typography>
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned Tests" />
        </Box>
      </Stack>
    </FormDialog>
  );
}
