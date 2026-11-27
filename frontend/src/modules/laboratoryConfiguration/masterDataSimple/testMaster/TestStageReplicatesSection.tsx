import { useEffect, useState } from "react";
import { TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Box, Typography, Chip, FormControl, InputLabel, TableContainer } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { useChangeControlGate } from "../../../../hooks/useChangeControlGate";
import { useTranslation } from "react-i18next";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, TestDefinitionStageReplicateDto, ProductionStageRole } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import { ALL_STAGE_ROLES } from "./testMasterConstants";

// sectionId is the laboratory of the test; it is what the change-control gate (CONF-227) checks and lists change controls for.
export function TestStageReplicatesSection({ testDefinitionId, sectionId }: { testDefinitionId: number; sectionId?: number | null }) {
  const { t } = useTranslation("masterData");
  const gate = useChangeControlGate({ sectionId: sectionId ?? null, recordKey: `test-${testDefinitionId}` });
  const [replicates, setReplicates] = useState<TestDefinitionStageReplicateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingReplicate, setEditingReplicate] = useState<TestDefinitionStageReplicateDto | null>(null);
  const [pendingDelete, setPendingDelete] = useState<TestDefinitionStageReplicateDto | null>(null);
  const [role, setRole] = useState<ProductionStageRole | "">("");
  const [sampleReplicates, setSampleReplicates] = useState("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadReplicates = () => {
    setLoading(true);
    setError(null);
    masterDataOptions
      .getTestDefinitionStageReplicates(testDefinitionId)
      .then(setReplicates)
      .catch((e: unknown) => {
        const errObj = e as { response?: { data?: { message?: string } }; message?: string };
        setError(errObj.response?.data?.message ?? errObj.message ?? t("testMaster.stageReplicates.errors.loadFailed"));
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadReplicates();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testDefinitionId]);

  const configuredRoles = new Set(replicates.map((r) => r.role));
  const availableRoles = ALL_STAGE_ROLES.filter((r) => !configuredRoles.has(r));

  const openAdd = () => {
    setEditingReplicate(null);
    setRole(availableRoles[0] ?? "");
    setSampleReplicates("");
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (r: TestDefinitionStageReplicateDto) => {
    setEditingReplicate(r);
    setRole(r.role);
    setSampleReplicates(String(r.sampleReplicates));
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSave = async () => {
    if (!editingReplicate && !role) {
      setDialogError(t("testMaster.stageReplicates.errors.roleRequired"));
      return;
    }

    const smpStr = sampleReplicates.trim();
    if (!smpStr) {
      setDialogError(t("testMaster.stageReplicates.errors.replicatesRequired"));
      return;
    }
    const smp = Number(smpStr);
    if (!Number.isInteger(smp) || smp < 1) {
      setDialogError(t("testMaster.stageReplicates.errors.replicatesInteger"));
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      if (editingReplicate) {
        const replicate = editingReplicate;
        await gate.run((cc) =>
          cc
            ? masterDataOptions.updateTestDefinitionStageReplicate(testDefinitionId, replicate.id, { sampleReplicates: smp }, replicate.version, cc)
            : masterDataOptions.updateTestDefinitionStageReplicate(testDefinitionId, replicate.id, { sampleReplicates: smp }, replicate.version)
        );
      } else {
        const created = { role: role as ProductionStageRole, sampleReplicates: smp };
        await gate.run((cc) =>
          cc ? masterDataOptions.createTestDefinitionStageReplicate(testDefinitionId, created, cc) : masterDataOptions.createTestDefinitionStageReplicate(testDefinitionId, created)
        );
      }
      setDialogOpen(false);
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? t("testMaster.stageReplicates.errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    setPendingDelete(null);
    setError(null);
    try {
      await gate.run((cc) =>
        cc ? masterDataOptions.deleteTestDefinitionStageReplicate(testDefinitionId, target.id, cc) : masterDataOptions.deleteTestDefinitionStageReplicate(testDefinitionId, target.id)
      );
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? t("testMaster.stageReplicates.errors.deleteFailed"));
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Stack sx={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          {t("testMaster.stageReplicates.heading", { count: replicates.length })}
        </Typography>
        <Button
          size="small"
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={openAdd}
          disabled={availableRoles.length === 0 || loading}
        >
          {t("testMaster.stageReplicates.addButton")}
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      <TableContainer>
        <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell>{t("testMaster.stageReplicates.columns.stageRole")}</TableCell>
              <TableCell>{t("testMaster.stageReplicates.columns.sampleReplicates")}</TableCell>
              <TableCell align="right">{t("testMaster.stageReplicates.columns.actions")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {replicates.map((r) => (
              <TableRow key={r.id}>
                <TableCell>
                  <Chip size="small" label={r.role} variant="outlined" sx={{ fontWeight: 600 }} />
                </TableCell>
                <TableCell>{r.sampleReplicates}</TableCell>
                <TableCell align="right">
                  <IconButton aria-label={t("testMaster.stageReplicates.editAria")} size="small" onClick={() => openEdit(r)} title={t("testMaster.stageReplicates.editTitle")}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton aria-label={t("testMaster.stageReplicates.deleteAria")}
                    size="small"
                    color="error"
                    onClick={() => setPendingDelete(r)}
                    title={t("testMaster.stageReplicates.deleteTitle")}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {replicates.length === 0 && !loading && (
              <TableRow>
                <TableCell colSpan={3} align="center" sx={{ py: 2, color: "text.secondary" }}>
                  {t("testMaster.stageReplicates.empty")}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
        {t("testMaster.stageReplicates.footnote")}
      </Typography>

      <FloatingDialog
        open={dialogOpen}
        title={editingReplicate ? t("testMaster.stageReplicates.dialog.editTitle", { role: editingReplicate.role }) : t("testMaster.stageReplicates.dialog.addTitle")}
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} variant="outlined" disabled={saving}>
              {t("testMaster.stageReplicates.dialog.cancel")}
            </Button>
            <Button variant="contained" onClick={handleSave} disabled={saving}>
              {saving ? t("testMaster.stageReplicates.dialog.saving") : editingReplicate ? t("testMaster.stageReplicates.dialog.saveChanges") : t("testMaster.stageReplicates.dialog.add")}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2} sx={{ pt: 1 }}>
          {editingReplicate ? (
            <TextField
              size="small"
              label={t("testMaster.stageReplicates.dialog.stageRoleLabel")}
              value={editingReplicate.role}
              disabled
              fullWidth
            />
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="stage-rep-role-select-label">{t("testMaster.stageReplicates.dialog.stageRoleLabel")}</InputLabel>
              <Select<ProductionStageRole>
                labelId="stage-rep-role-select-label"
                label={t("testMaster.stageReplicates.dialog.stageRoleLabel")}
                value={role as ProductionStageRole}
                onChange={(e) => setRole(e.target.value as ProductionStageRole)}
                inputProps={{ "aria-label": t("testMaster.stageReplicates.dialog.stageRoleLabel") }}
              >
                {availableRoles.map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          <TextField
            size="small"
            type="number"
            label={t("testMaster.stageReplicates.dialog.sampleReplicatesLabel")}
            placeholder={t("testMaster.stageReplicates.dialog.sampleReplicatesPlaceholder")}
            value={sampleReplicates}
            onChange={(e) => setSampleReplicates(e.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText={t("testMaster.stageReplicates.dialog.sampleReplicatesHelper")}
            required
            fullWidth
          />
        </Stack>
      </FloatingDialog>

      <ConfirmationDialog
        open={pendingDelete != null}
        title={t("testMaster.stageReplicates.deleteDialog.title")}
        message={
          pendingDelete
            ? t("testMaster.stageReplicates.deleteDialog.message", { role: pendingDelete.role })
            : ""
        }
        confirmText={t("testMaster.stageReplicates.deleteDialog.confirm")}
        destructive
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
      {gate.prompt}
    </Box>
  );
}
