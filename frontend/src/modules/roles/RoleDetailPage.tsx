import { useEffect, useMemo, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Paper, TextField, Button, Stack, Typography, Alert, Chip, Box,
  List, ListItem, ListItemText, Divider
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { ConfirmationDialog } from "../../components/ConfirmationDialog";
import { RoleService, RoleDetail, PermissionRecord } from "./services/RoleService";
import { UserService, UserRecord } from "../users/services/UserService";
import { PermissionMatrix } from "./components/PermissionMatrix";

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

  const load = () => {
    RoleService.getById(roleId).then((r) => {
      setRole(r);
      setName(r.name);
      setDescription(r.description ?? "");
      setCheckedCodes(new Set(r.permissionCodes));
    }).catch(() => setMessage({ text: "Could not load this role.", ok: false }));
    RoleService.getAllPermissions().then(setPermissions).catch(() => {});
    UserService.getAll().then(setUsers).catch(() => {});
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [roleId]);

  const assignedUsers = useMemo(() => users.filter((u) => u.roleId === roleId), [users, roleId]);

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
      await RoleService.update(role.id, name, description || null);
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
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 1 }}>
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

      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
          {role.isSystemRole && <Chip label="System Role" size="small" color="secondary" />}
          <Chip label={`Base Type: ${role.type}`} size="small" variant="outlined" />
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

      <Box sx={{ mt: 3 }}>
        <Button variant="contained" onClick={handleSave} disabled={saving}>
          {saving ? "Saving..." : "Save Changes"}
        </Button>
      </Box>

      <SectionTitle>{`Assigned Users (${assignedUsers.length})`}</SectionTitle>
      <Paper>
        {assignedUsers.length === 0 ? (
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
                  <Chip label={u.isActive ? "Active" : "Disabled"} size="small" color={u.isActive ? "success" : "default"} />
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
