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
import { ConfirmationDialog } from "../../components/ConfirmationDialog";
import { RoleService, RoleDetail, PermissionRecord } from "./services/RoleService";
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
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [checkedCodes, setCheckedCodes] = useState<Set<string>>(new Set());
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

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

  const handleSave = async () => {
    if (!role) return;
    if (!name.trim()) {
      setMessage({ text: "Role name is required.", ok: false });
      return;
    }
    setSaving(true);
    setMessage(null);
    try {
      await RoleService.update(role.id, name, description || null, role.version);
      await RoleService.updatePermissions(role.id, Array.from(checkedCodes));
      setMessage({ text: "Role saved.", ok: true });
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this role.", ok: false });
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!role) return;
    setDeleting(true);
    try {
      await RoleService.remove(role.id);
      navigate("/roles", { state: { message: `Role "${role.name}" deleted.` } });
    } catch (e: any) {
      setConfirmDelete(false);
      setDeleting(false);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this role.", ok: false });
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
            onClick={() => setConfirmDelete(true)}
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

      {!role.isSystemRole && (
        <Alert severity="info" sx={{ mb: 3 }}>
          This role behaves like <strong>{role.type}</strong> on screens not yet migrated to the new permission system
          (marked "Legacy-only" below). Custom permissions take full effect on everything marked "Enforced".
        </Alert>
      )}

      <SectionTitle>Permissions</SectionTitle>
      <PermissionMatrix permissions={permissions} checkedCodes={checkedCodes} onToggle={handleToggle} />

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
        <Button variant="outlined" color="inherit" onClick={handleDiscard} disabled={!isDirty || saving}>
          Discard Changes
        </Button>
        <Button variant="contained" onClick={handleSave} disabled={!isDirty || saving}>
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

      <ConfirmationDialog
        open={confirmDelete}
        message={`Delete role "${role.name}"? This cannot be undone.${assignedUsers.length > 0 ? ` It is currently assigned to ${assignedUsers.length} user(s) and cannot be deleted until they are reassigned.` : ""}`}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={handleDelete}
      />
    </>
  );
}
