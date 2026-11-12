import { useState } from "react";
import { Alert, Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { Item, ItemSaveRequest, ItemService } from "../services/ItemService";

interface ItemAssignedTestsSectionProps {
  item: Item;
  // A viewer who may not manage items sees the list without the edit control.
  readOnly?: boolean;
  onSaved?: () => void;
}

// The item's Assigned Tests tab: the tests ordered automatically when a sample
// of this item is received, editable in place. Saving sends the whole item
// back with its row version, exactly as the Edit Item dialog does; the server
// decides whether a test may be added or removed.
export function ItemAssignedTestsSection({ item, readOnly = false, onSaved }: ItemAssignedTestsSectionProps) {
  const assignedTests = item.assignedTests ?? [];
  const [editing, setEditing] = useState(false);
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEdit = () => {
    setTestCodes(assignedTests.map((t) => t.testCode));
    setError(null);
    setEditing(true);
  };

  const save = async () => {
    setError(null);
    if (testCodes.length === 0) {
      setError("At least one assigned test is required.");
      return;
    }
    const payload: ItemSaveRequest = {
      name: item.name,
      code: item.code,
      category: item.category,
      sopNumber: item.sopNumber,
      assignedTests: testCodes.map((code) => ({
        testCode: code,
        displayName: assignedTests.find((t) => t.testCode === code)?.displayName ?? code
      })),
      minSampleQuantity: item.minSampleQuantity,
      minRetestSampleQuantity: item.minRetestSampleQuantity,
      minSampleQuantityUnit: item.minSampleQuantityUnit
    };
    setSaving(true);
    try {
      await ItemService.update(item.id, payload, item.version);
      setEditing(false);
      onSaved?.();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not save the assigned tests.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", justifyContent: "space-between" }}>
        <Alert severity="info" sx={{ py: 0.5, fontSize: 12, flex: 1 }}>
          These tests are automatically assigned when a new sample is received for <strong>{item.name}</strong>.
        </Alert>
        {!readOnly && !editing && (
          <Button size="small" variant="outlined" startIcon={<EditOutlinedIcon fontSize="small" />} onClick={startEdit} sx={{ textTransform: "none", flexShrink: 0 }}>
            Edit tests
          </Button>
        )}
      </Stack>

      {editing ? (
        <Stack spacing={1.5}>
          {error && <Alert severity="error">{error}</Alert>}
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned Tests" />
          <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
            <Button onClick={() => setEditing(false)} disabled={saving} color="inherit" sx={{ textTransform: "none" }}>
              Cancel
            </Button>
            <Button variant="contained" onClick={() => { void save(); }} disabled={saving} sx={{ textTransform: "none", fontWeight: 700 }}>
              Save tests
            </Button>
          </Stack>
        </Stack>
      ) : assignedTests.length > 0 ? (
        <Box component="ul" aria-label="Assigned tests" sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5, listStyle: "none", m: 0, p: 0 }}>
          {assignedTests.map((t) => (
            <Paper
              component="li"
              key={t.testCode}
              sx={{
                p: 1.5,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1,
                bgcolor: "background.default",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between"
              }}
            >
              <Typography variant="body2" sx={{ fontWeight: 700, color: "text.primary" }}>
                {t.displayName || t.testCode}
              </Typography>
              <Chip label={t.testCode} size="small" sx={{ fontSize: 12, height: 20, fontWeight: 600 }} />
            </Paper>
          ))}
        </Box>
      ) : (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          No tests assigned to this item yet.
        </Typography>
      )}
    </Stack>
  );
}
