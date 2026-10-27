import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Paper, TextField, Button, Stack, Typography, Alert, Box, Stepper, Step, StepLabel,
  RadioGroup, FormControlLabel, Radio, Select, MenuItem, Divider, Chip
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PageHeader } from "../../components/PageHeader";
import { SignatureDialog } from "../../components/SignatureDialog";
import { RoleService, RoleRecord, PermissionRecord, SodRules } from "./services/RoleService";
import { PermissionMatrix } from "./components/PermissionMatrix";
import { useLoadFailures } from "../../hooks/useLoadFailures";
import { Role as RoleType } from "../../contexts/AuthContext";
import { LoadFailuresAlert } from "../../components/LoadErrorAlert";

const BASE_TYPES: RoleType[] = ["SystemAdministrator", "SectionHead", "Reviewer", "Analyst"];
const STEPS = ["Name", "Base Type", "Permissions", "Review"];

export function CreateRolePage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);

  const [existingRoles, setExistingRoles] = useState<RoleRecord[]>([]);
  const [allPermissions, setAllPermissions] = useState<PermissionRecord[]>([]);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [baseType, setBaseType] = useState<RoleType>("Analyst");
  const [checkedCodes, setCheckedCodes] = useState<Set<string>>(new Set());
  const [cloneFromRoleId, setCloneFromRoleId] = useState<number | "">("");

  const [sodRules, setSodRules] = useState<SodRules | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);
  // Kept so a refused password can be retried without creating the role twice.
  const [createdId, setCreatedId] = useState<number | null>(null);

  const { failed: failedLists, fail } = useLoadFailures();

  useEffect(() => {
    RoleService.getAll().then(setExistingRoles).catch(fail("the existing roles"));
    RoleService.getAllPermissions().then(setAllPermissions).catch(fail("the permission list"));
    RoleService.getSodRules().then(setSodRules).catch(fail("the segregation-of-duties rules"));
  }, [fail]);

  // Default-check the selected base type's current grants whenever it
  // changes, fetched live rather than hardcoded so this never drifts
  // from what the base role actually holds.
  const applyBaseTypeDefaults = async (type: RoleType) => {
    const baseRole = existingRoles.find((r) => r.isSystemRole && r.type === type);
    if (!baseRole) return;
    try {
      const detail = await RoleService.getById(baseRole.id);
      setCheckedCodes(new Set(detail.permissionCodes));
    } catch {
      // Leave whatever was selected before - not worth blocking the wizard over.
    }
  };

  const handleBaseTypeChange = (type: RoleType) => {
    setBaseType(type);
    setCloneFromRoleId("");
    applyBaseTypeDefaults(type);
  };

  const handleCloneFrom = async (roleId: number) => {
    setCloneFromRoleId(roleId);
    try {
      const detail = await RoleService.getById(roleId);
      setCheckedCodes(new Set(detail.permissionCodes));
    } catch {
      // Leave the matrix as-is on failure.
    }
  };

  const handleToggle = (code: string, checked: boolean) => {
    setCheckedCodes((prev) => {
      const next = new Set(prev);
      if (checked) next.add(code); else next.delete(code);
      return next;
    });
  };

  const canAdvanceFromStep = (s: number) => {
    if (s === 0) return name.trim().length > 0;
    return true;
  };

  // The permission grant is electronically signed, so creating opens the signature step.
  const handleSubmit = () => {
    if (!reason.trim()) {
      setError("A reason is required to create a role.");
      return;
    }
    setError(null);
    setSigning(true);
  };

  // Errors are thrown back to the signature dialog, which shows them and stays open.
  const confirmSignature = async (password: string) => {
    setSubmitting(true);
    try {
      let id = createdId;
      if (id === null) {
        const created = await RoleService.create(name, description || null, baseType, reason.trim());
        id = created.id;
        setCreatedId(id);
      }
      await RoleService.updatePermissions(id, Array.from(checkedCodes), reason.trim(), password);
      setSigning(false);
      navigate(`/roles/${id}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/roles")} sx={{ mb: 1 }}>
        Back to Roles
      </Button>
      <PageHeader title="Create Role" subtitle="Define a new role's identity, base type, and granted permissions." />

      <Stepper activeStep={step} sx={{ mb: 3 }}>
        {STEPS.map((label) => <Step key={label}><StepLabel>{label}</StepLabel></Step>)}
      </Stepper>

      <LoadFailuresAlert failed={failedLists} retryHint="Reload the page to try again." sx={{ mb: 2 }} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {step === 0 && (
        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2} sx={{ maxWidth: 480 }}>
            <TextField label="Role Name" size="small" value={name} onChange={(e) => setName(e.target.value)} autoFocus required fullWidth />
            <TextField label="Description" size="small" value={description} onChange={(e) => setDescription(e.target.value)} multiline rows={3} fullWidth />
          </Stack>
        </Paper>
      )}

      {step === 1 && (
        <Paper sx={{ p: 2.5 }}>
          <Alert severity="info" sx={{ mb: 2.5 }}>
            New roles are currently based on one of the 4 built-in types for compatibility with parts of the system
            not yet migrated to the new permission model. Permissions marked "Enforced" in the next step apply fully;
            everything else follows <strong>{baseType}</strong>'s existing behavior.
          </Alert>
          <RadioGroup value={baseType} onChange={(e) => handleBaseTypeChange(e.target.value as RoleType)}>
            {BASE_TYPES.map((t) => (
              <FormControlLabel key={t} value={t} control={<Radio />} label={t} />
            ))}
          </RadioGroup>
        </Paper>
      )}

      {step === 2 && (
        <>
          <Paper sx={{ p: 2.5, mb: 2 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 600, mb: 1 }}>
              Starting point: {baseType}'s current permissions (edit freely below)
            </Typography>
            <Stack direction="row" spacing={1.5} sx={{
              alignItems: "center"
            }}>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>Or clone permissions from any existing role:</Typography>
              <Select
                size="small"
                displayEmpty
                value={cloneFromRoleId}
                onChange={(e) => handleCloneFrom(Number(e.target.value))}
                sx={{ minWidth: 220 }}
                inputProps={{ "aria-label": "Role to clone" }}
              >
                <MenuItem value=""><em>Select a role to clone</em></MenuItem>
                {existingRoles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
              </Select>
            </Stack>
          </Paper>
          <PermissionMatrix permissions={allPermissions} checkedCodes={checkedCodes} onToggle={handleToggle} sodRules={sodRules} />
        </>
      )}

      {step === 3 && (
        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>Name</Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 700 }}>{name}</Typography>
            </Box>
            {description && (
              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>Description</Typography>
                <Typography sx={{ fontSize: 14 }}>{description}</Typography>
              </Box>
            )}
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>Base Type</Typography>
              <Typography sx={{ fontSize: 14 }}>{baseType}</Typography>
            </Box>
            <Divider />
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase", mb: 1 }}>
                Permissions ({checkedCodes.size})
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap sx={{
                flexWrap: "wrap"
              }}>
                {Array.from(checkedCodes).sort().map((c) => <Chip key={c} label={c} size="small" />)}
                {checkedCodes.size === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>None selected.</Typography>}
              </Stack>
            </Box>
            <Divider />
            <TextField label="Reason for Creating This Role" size="small" value={reason} onChange={(e) => setReason(e.target.value)} required fullWidth multiline rows={2} placeholder="Mandatory administrative justification" />
          </Stack>
        </Paper>
      )}

      <Stack direction="row" spacing={1.5} sx={{ mt: 3 }}>
        <Button disabled={step === 0} onClick={() => setStep((s) => s - 1)}>Back</Button>
        {step < STEPS.length - 1 ? (
          <Button variant="contained" disabled={!canAdvanceFromStep(step)} onClick={() => setStep((s) => s + 1)}>
            Next
          </Button>
        ) : (
          <Button variant="contained" color="primary" disabled={submitting || !reason.trim()} onClick={handleSubmit}>
            {submitting ? "Creating..." : "Create Role"}
          </Button>
        )}
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement={`I am creating the role "${name}" and granting it ${checkedCodes.size} permission(s). Reason: ${reason.trim()}`}
        onCancel={() => setSigning(false)}
        onConfirm={confirmSignature}
      />
    </>
  );
}
