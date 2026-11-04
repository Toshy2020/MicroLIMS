import { useEffect, useMemo, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Paper, TextField, Button, Stack, Typography, Alert, Box,
  List, ListItem, ListItemText, Divider
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { RoleService, RoleDetail, PermissionRecord, SodRules } from "./services/RoleService";
import { UserService, UserRecord } from "../users/services/UserService";
import { useLoadFailures } from "../../hooks/useLoadFailures";
import { PermissionMatrix } from "./components/PermissionMatrix";
import { LoadFailuresAlert } from "../../components/LoadErrorAlert";
import { MetaBadge, ToneBadge } from "../../components/StatusBadge";

export function RoleDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const roleId = Number(id);

  const [role, setRole] = useState<RoleDetail | null>(null);
  const [permissions, setPermissions] = useState<PermissionRecord[]>([]);
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [sodRules, setSodRules] = useState<SodRules | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [checkedCodes, setCheckedCodes] = useState<Set<string>>(new Set());
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [reason, setReason] = useState("");
  const [deleteReason, setDeleteReason] = useState("");
  // Permission edits and role deletion are electronically signed (password re-entry).
  const [signing, setSigning] = useState<"permissions" | "delete" | null>(null);

  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();

  const load = () => {
    resetFailures();
    RoleService.getById(roleId).then((r) => {
      setRole(r);
      setName(r.name);
      setDescription(r.description ?? "");
      setCheckedCodes(new Set(r.permissionCodes));
    }).catch(() => setMessage({ text: "Could not load this role.", ok: false }));
    RoleService.getAllPermissions().then(setPermissions).catch(fail("the permission list"));
    UserService.getAll().then(setUsers).catch(fail("the users holding this role"));
    RoleService.getSodRules().then(setSodRules).catch(fail("the segregation-of-duties rules"));
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [roleId]);

  const usersFailed = failedLists.includes("the users holding this role");
  const assignedUsers = useMemo(() => users.filter((u) => u.roleId === roleId), [users, roleId]);

  // Unsaved edits, compared against the role as last loaded - drives the
  // sticky action bar so Save/Discard stay in reach below a long matrix.
  const isDirty = useMemo(() => {
    if (!role) return false;
    if (name !== role.name || description !== (role.description ?? "")) return true;
    if (checkedCodes.size !== role.permissionCodes.length) return true;
    return role.permissionCodes.some((c) => !checkedCodes.has(c));
  }, [role, name, description, checkedCodes]);

  const handleDiscard = () => {
    if (!role) return;
    setName(role.name);
    setDescription(role.description ?? "");
    setCheckedCodes(new Set(role.permissionCodes));
    setMessage(null);
  };

  const handleToggle = (code: string, checked: boolean) => {
    setCheckedCodes((prev) => {
      const next = new Set(prev);
      if (checked) next.add(code); else next.delete(code);
      return next;
    });
  };

  const permissionsChanged = useMemo(() => {
    if (!role) return false;
    if (checkedCodes.size !== role.permissionCodes.length) return true;
    return role.permissionCodes.some((c) => !checkedCodes.has(c));
  }, [role, checkedCodes]);

  const saveRename = async () => {
    if (!role) return;
    if (name !== role.name || description !== (role.description ?? "")) {
      await RoleService.update(role.id, name, description || null, reason.trim(), role.version);
    }
  };

  const handleSave = async () => {
    if (!role) return;
    if (!name.trim()) {
      setMessage({ text: "Role name is required.", ok: false });
      return;
    }
    if (!reason.trim()) {
      setMessage({ text: "A reason is required to save changes to a role.", ok: false });
      return;
    }
    setMessage(null);
    // A permission edit is signed first; a rename alone needs only the reason.
    if (permissionsChanged) {
      setSigning("permissions");
      return;
    }
    setSaving(true);
    try {
      await saveRename();
      setMessage({ text: "Role saved.", ok: true });
      setReason("");
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this role.", ok: false });
    } finally {
      setSaving(false);
    }
  };

  // Errors are thrown back to the signature dialog, which shows them and stays open.
  const confirmPermissionsSignature = async (password: string) => {
    if (!role) return;
    setSaving(true);
    try {
      await RoleService.updatePermissions(role.id, Array.from(checkedCodes), reason.trim(), password);
      await saveRename();
      setSigning(null);
      setMessage({ text: "Role saved.", ok: true });
      setReason("");
      load();
    } finally {
      setSaving(false);
    }
  };

  const confirmDeleteSignature = async (password: string) => {
    if (!role) return;
    setDeleting(true);
    try {
      await RoleService.remove(role.id, deleteReason.trim(), password);
      navigate("/roles", { state: { message: `Role "${role.name}" deleted.` } });
    } catch (e: any) {
      setDeleting(false);
      const msg: string = e?.response?.data?.message ?? "Could not delete this role.";
      // Only a refused password stays in the signature dialog.
      if (/password|signature/i.test(msg)) throw e;
      setSigning(null);
      setConfirmDelete(false);
      setMessage({ text: msg, ok: false });
    }
  };

  if (!role) {
    return (
      <>
        <PageHeader title="Role" />
        {message && <Alert severity="error">{message.text}</Alert>}
      </>
    );
  }

  return (
    <>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/roles")} sx={{ mb: 1 }}>
        Back to Roles
      </Button>
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "flex-start",
          mb: 1
        }}>
        <PageHeader title={role.name} subtitle="Edit this role's name, description, and granted permissions." />
        {!role.isSystemRole && (
          <Button
            startIcon={<DeleteIcon />}
            color="error"
            onClick={() => { setDeleteReason(""); setConfirmDelete(true); }}
            disabled={deleting}
            sx={{ flexShrink: 0 }}
          >
            {deleting ? "Deleting..." : "Delete Role"}
          </Button>
        )}
      </Stack>
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>{message.text}</Alert>}
      <LoadFailuresAlert failed={failedLists} retryHint="Reload the page to try again." sx={{ mb: 2 }} />

      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{
            alignItems: "center",
            mb: 2
          }}>
          {role.isSystemRole && <MetaBadge label="System Role" locked />}
          <MetaBadge label={`Base Type: ${role.type}`} />
        </Stack>
        <Stack spacing={2}>
          <TextField label="Name" size="small" value={name} onChange={(e) => setName(e.target.value)} fullWidth />
          <TextField label="Description" size="small" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth multiline rows={2} />
        </Stack>
      </Paper>

      {role.sodConflicts && role.sodConflicts.length > 0 && (
        <Alert severity="warning" sx={{ mb: 3 }}>
          This role mixes system administration with GxP execution or approval ({role.sodConflicts.join("; ")}).
          Segregation of duties does not allow that: uncheck one side of each pair and save.
        </Alert>
      )}

      {!role.isSystemRole && (
        <Alert severity="info" sx={{ mb: 3 }}>
          This role behaves like <strong>{role.type}</strong> on screens not yet migrated to the new permission system
          (marked "Legacy-only" below). Custom permissions take full effect on everything marked "Enforced".
        </Alert>
      )}

      <SectionTitle>Permissions</SectionTitle>
      <PermissionMatrix permissions={permissions} checkedCodes={checkedCodes} onToggle={handleToggle} sodRules={sodRules} />

      <Box
        sx={{
          position: "sticky", bottom: 0, zIndex: 2, mt: 3, py: 1.25, px: 2,
          display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 1.5,
          bgcolor: "background.paper", borderTop: "1px solid", borderColor: "divider"
        }}
      >
        <Typography role="status" sx={{ fontSize: 13, color: "text.secondary", mr: "auto" }}>
          {isDirty ? "You have unsaved changes." : "No unsaved changes."}
        </Typography>
        {isDirty && (
          <TextField label="Reason for Change" size="small" value={reason} onChange={(e) => setReason(e.target.value)} required sx={{ minWidth: 280 }} />
        )}
        <Button variant="outlined" color="inherit" onClick={handleDiscard} disabled={!isDirty || saving}>
          Discard Changes
        </Button>
        <Button variant="contained" onClick={handleSave} disabled={!isDirty || saving || !reason.trim()}>
          {saving ? "Saving..." : "Save Changes"}
        </Button>
      </Box>

      <SectionTitle>{usersFailed ? "Assigned Users" : `Assigned Users (${assignedUsers.length})`}</SectionTitle>
      <Paper>
        {usersFailed ? (
          <Typography sx={{ fontSize: 13, color: "error.main", p: 2 }}>The users holding this role could not be loaded.</Typography>
        ) : assignedUsers.length === 0 ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary", p: 2 }}>No users currently hold this role.</Typography>
        ) : (
          <List dense disablePadding>
            {assignedUsers.map((u, i) => (
              <Box key={u.id}>
                {i > 0 && <Divider />}
                <ListItem>
                  <ListItemText
                    primary={u.fullName}
                    secondary={`@${u.username}${u.email ? ` · ${u.email}` : ""}`}
                  />
                  <ToneBadge tone={u.isActive ? "notDetected" : "pending"} label={u.isActive ? "Active" : "Disabled"} />
                </ListItem>
              </Box>
            ))}
          </List>
        )}
      </Paper>

      <FloatingDialog
        open={confirmDelete && signing === null}
        onClose={() => setConfirmDelete(false)}
        maxWidth="xs"
        title="Delete Role"
        actions={
          <>
            <Button onClick={() => setConfirmDelete(false)}>Cancel</Button>
            <Button variant="contained" color="error" disabled={!deleteReason.trim()} onClick={() => setSigning("delete")}>
              Continue to Sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            {`Delete role "${role.name}"? This cannot be undone.${assignedUsers.length > 0 ? ` It is currently assigned to ${assignedUsers.length} user(s) and cannot be deleted until they are reassigned.` : ""}`}
          </Typography>
          <TextField label="Reason for Deletion" size="small" value={deleteReason} onChange={(e) => setDeleteReason(e.target.value)} required fullWidth multiline rows={2} placeholder="Mandatory justification" />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing === "permissions"}
        meaningStatement={`I am changing the permissions of the role "${role.name}". Reason: ${reason.trim()}`}
        onCancel={() => setSigning(null)}
        onConfirm={confirmPermissionsSignature}
      />
      <SignatureDialog
        open={signing === "delete"}
        meaningStatement={`I am deleting the role "${role.name}". Reason: ${deleteReason.trim()}`}
        onCancel={() => setSigning(null)}
        onConfirm={confirmDeleteSignature}
      />
    </>
  );
}
