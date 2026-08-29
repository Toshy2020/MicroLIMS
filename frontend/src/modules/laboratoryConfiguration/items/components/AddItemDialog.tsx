import { useState, useEffect } from "react";
import {
  Button,
  TextField,
  Select,
  MenuItem,
  Stack,
  Box,
  Typography,
  Alert,
} from "@mui/material";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { Item } from "../services/ItemService";
import { FloatingDialog } from "../../../../components/FloatingDialog";

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
    setError(null);
  }, [itemToEdit, open]);

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
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to save item.");
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
            <TextField
              size="small"
              label="SOP Number"
              value={sopNumber}
              onChange={(e) => setSopNumber(e.target.value)}
              sx={{ flex: 1 }}
            />
          </Stack>

          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600, display: "block", mb: 0.5 }}>
              Category
            </Typography>
            <Select size="small" value={category} onChange={(e) => setCategory(e.target.value)} fullWidth>
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
