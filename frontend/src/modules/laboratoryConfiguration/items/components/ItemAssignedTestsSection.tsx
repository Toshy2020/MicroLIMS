import { useState } from "react";
import { Alert, Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { TestCodePickerMulti } from "../../../../components/TestCodePickerMulti";
import { Item, ItemSaveRequest, ItemService } from "../services/ItemService";
import { isReasonRequiredError } from "../../../../services/apiClient";
import { ChangeReasonDialog } from "../../../../components/config/ChangeReasonDialog";

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
  const { t } = useTranslation("labConfig");
  const assignedTests = item.assignedTests ?? [];
  const [editing, setEditing] = useState(false);
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingPayload, setPendingPayload] = useState<ItemSaveRequest | null>(null);
  const [reasonError, setReasonError] = useState<string | null>(null);

  const startEdit = () => {
    setTestCodes(assignedTests.map((t) => t.testCode));
    setError(null);
    setEditing(true);
  };

  const buildPayload = (): ItemSaveRequest => ({
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
  });

  const save = async () => {
    setError(null);
    if (testCodes.length === 0) {
      setError(t("items.assignedTests.errors.atLeastOne"));
      return;
    }
    const payload = buildPayload();
    setSaving(true);
    try {
      await ItemService.update(item.id, payload, item.version);
      setEditing(false);
      onSaved?.();
    } catch (e: unknown) {
      if (isReasonRequiredError(e)) {
        setReasonError(null);
        setPendingPayload(payload);
        return;
      }
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("items.assignedTests.errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const confirmReasonSave = async (reason: string) => {
    if (!pendingPayload) return;
    setSaving(true);
    setReasonError(null);
    try {
      await ItemService.update(item.id, pendingPayload, item.version, reason);
      setPendingPayload(null);
      setEditing(false);
      onSaved?.();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setReasonError(err?.response?.data?.message ?? t("items.assignedTests.errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", justifyContent: "space-between" }}>
        <Alert severity="info" sx={{ py: 0.5, fontSize: 12, flex: 1 }}>
          <Trans
            i18nKey="items.assignedTests.infoBanner"
            ns="labConfig"
            values={{ name: item.name }}
            components={{ bold: <strong /> }}
          />
        </Alert>
        {!readOnly && !editing && (
          <Button size="small" variant="outlined" startIcon={<EditOutlinedIcon fontSize="small" />} onClick={startEdit} sx={{ textTransform: "none", flexShrink: 0 }}>
            {t("items.assignedTests.editTests")}
          </Button>
        )}
      </Stack>

      {editing ? (
        <Stack spacing={1.5}>
          {error && <Alert severity="error">{error}</Alert>}
          <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label={t("items.assignedTests.pickerLabel")} />
          <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
            <Button onClick={() => setEditing(false)} disabled={saving} color="inherit" sx={{ textTransform: "none" }}>
              {t("items.assignedTests.cancel")}
            </Button>
            <Button variant="contained" onClick={() => { void save(); }} disabled={saving} sx={{ textTransform: "none", fontWeight: 700 }}>
              {t("items.assignedTests.saveTests")}
            </Button>
          </Stack>
        </Stack>
      ) : assignedTests.length > 0 ? (
        <Box component="ul" aria-label={t("items.assignedTests.listAriaLabel")} sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1.5, listStyle: "none", m: 0, p: 0 }}>
          {assignedTests.map((tItem) => (
            <Paper
              component="li"
              key={tItem.testCode}
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
                {tItem.displayName || tItem.testCode}
              </Typography>
              <Chip label={tItem.testCode} size="small" sx={{ fontSize: 12, height: 20, fontWeight: 600 }} />
            </Paper>
          ))}
        </Box>
      ) : (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("items.assignedTests.empty")}
        </Typography>
      )}
      <ChangeReasonDialog
        open={pendingPayload !== null}
        title={t("items.assignedTests.reasonTitle", { name: item.name })}
        confirmText={t("items.assignedTests.saveTests")}
        loading={saving}
        error={reasonError}
        onCancel={() => { setPendingPayload(null); setReasonError(null); }}
        onConfirm={confirmReasonSave}
      />
    </Stack>
  );
}
