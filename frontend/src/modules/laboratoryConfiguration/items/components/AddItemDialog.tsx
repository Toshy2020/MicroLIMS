import { useState, useEffect, useRef } from "react";
import {
  Button,
  TextField,
  Select,
  MenuItem,
  Stack,
  Box,
  Typography,
  Alert,
  Autocomplete,
  AutocompleteInputChangeReason,
} from "@mui/material";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { Item } from "../services/ItemService";
import { FloatingDialog } from "../../../../components/FloatingDialog";
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
    } else {
      setName("");
      setCode("");
      setSopNumber("");
      setCategory("FinishedProduct");
      setTestCodes([]);
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
      await onSave({
        name: name.trim(),
        code: code.trim(),
        category,
        sopNumber: sopNumber.trim(),
        testCodes,
      });
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
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      titleSx={{ fontWeight: 700, fontSize: 16 }}
      title={itemToEdit ? `Edit Item: ${itemToEdit.name}` : "Add New Item"}
      actions={
        <>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button variant="contained" onClick={handleSubmit} disabled={saving}>
            {saving ? "Saving..." : itemToEdit ? "Save Changes" : "Save Item"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 0.5 }}>
        {error && <Alert severity="error">{error}</Alert>}

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

        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
            Assigned Tests (Auto-assigned on sample receipt)
          </Typography>
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned Tests" />
        </Box>
      </Stack>
    </FloatingDialog>
  );
}
