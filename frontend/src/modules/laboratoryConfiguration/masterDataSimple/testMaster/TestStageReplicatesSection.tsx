import { useEffect, useState } from "react";
import { TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Alert, IconButton, Select, MenuItem, Box, Typography, Chip, FormControl, InputLabel, TableContainer } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { masterDataOptions, TestDefinitionStageReplicateDto, ProductionStageRole } from "../../../../services/masterDataOptions";
import { tableHeadSx } from "../../../../theme";
import { ALL_STAGE_ROLES } from "./testMasterConstants";

export function TestStageReplicatesSection({ testDefinitionId }: { testDefinitionId: number }) {
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
        setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load stage replicates.");
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
      setDialogError("Production stage role is required.");
      return;
    }

    const smpStr = sampleReplicates.trim();
    if (!smpStr) {
      setDialogError("Sample replicates is required.");
      return;
    }
    const smp = Number(smpStr);
    if (!Number.isInteger(smp) || smp < 1) {
      setDialogError("Sample replicates must be an integer greater than or equal to 1.");
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      if (editingReplicate) {
        await masterDataOptions.updateTestDefinitionStageReplicate(testDefinitionId, editingReplicate.id, {
          sampleReplicates: smp
        }, editingReplicate.version);
      } else {
        await masterDataOptions.createTestDefinitionStageReplicate(testDefinitionId, {
          role: role as ProductionStageRole,
          sampleReplicates: smp
        });
      }
      setDialogOpen(false);
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errObj.response?.data?.message ?? errObj.message ?? "Could not save stage replicate configuration.");
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
      await masterDataOptions.deleteTestDefinitionStageReplicate(testDefinitionId, target.id);
      loadReplicates();
    } catch (e: unknown) {
      const errObj = e as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? "Could not delete stage replicate configuration.");
    }
  };

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Stack sx={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
          Replicates per stage ({replicates.length} configured)
        </Typography>
        <Button
          size="small"
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={openAdd}
          disabled={availableRoles.length === 0 || loading}
        >
          Add Stage Replicate
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert>}

      <TableContainer>
        <Table size="small" sx={{ bgcolor: "background.paper", borderRadius: 1 }}>
          <TableHead>
            <TableRow sx={tableHeadSx}>
              <TableCell>Stage Role</TableCell>
              <TableCell>Sample Replicates</TableCell>
              <TableCell align="right">Actions</TableCell>
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
                  <IconButton aria-label="Edit replicates" size="small" onClick={() => openEdit(r)} title="Edit Replicates">
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton aria-label="Delete replicate rule"
                    size="small"
                    color="error"
                    onClick={() => setPendingDelete(r)}
                    title="Delete Replicate Configuration"
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {replicates.length === 0 && !loading && (
              <TableRow>
                <TableCell colSpan={3} align="center" sx={{ py: 2, color: "text.secondary" }}>
                  No stage replicates configured yet. Click "Add Stage Replicate" to configure replicate counts.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mt: 1 }}>
        Production stages without a configured row are not configured for this test (not zero). Counts must be integers ≥ 1. HPLC standard injections are set on the method.
      </Typography>

      <FloatingDialog
        open={dialogOpen}
        title={editingReplicate ? `Edit Stage Replicates: ${editingReplicate.role}` : "Add Stage Replicate"}
        onClose={() => setDialogOpen(false)}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button variant="contained" onClick={handleSave} disabled={saving}>
              {saving ? "Saving…" : editingReplicate ? "Save Changes" : "Add"}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2} sx={{ pt: 1 }}>
          {editingReplicate ? (
            <TextField
              size="small"
              label="Stage Role"
              value={editingReplicate.role}
              disabled
              fullWidth
            />
          ) : (
            <FormControl size="small" fullWidth required>
              <InputLabel id="stage-rep-role-select-label">Stage Role</InputLabel>
              <Select<ProductionStageRole>
                labelId="stage-rep-role-select-label"
                label="Stage Role"
                value={role as ProductionStageRole}
                onChange={(e) => setRole(e.target.value as ProductionStageRole)}
                inputProps={{ "aria-label": "Stage Role" }}
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
            label="Sample Replicates *"
            placeholder="e.g. 2"
            value={sampleReplicates}
            onChange={(e) => setSampleReplicates(e.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText="Integer ≥ 1"
            required
            fullWidth
          />
        </Stack>
      </FloatingDialog>

      <ConfirmationDialog
        open={pendingDelete != null}
        title="Delete Stage Replicate Configuration"
        message={
          pendingDelete
            ? `Delete stage replicate configuration for role "${pendingDelete.role}"?`
            : ""
        }
        confirmText="Delete"
        destructive
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </Box>
  );
}
