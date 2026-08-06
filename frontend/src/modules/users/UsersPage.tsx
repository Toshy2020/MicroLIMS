import { useEffect, useState } from "react";
import { Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, Box, IconButton } from "@mui/material";
import BlockIcon from "@mui/icons-material/Block";
import EditIcon from "@mui/icons-material/Edit";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService, RoleRecord } from "../roles/services/RoleService";
import { StatusBadge } from "../../components/StatusBadge";

export function UsersPage() {
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);

  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [roleId, setRoleId] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [editingEmailId, setEditingEmailId] = useState<number | null>(null);
  const [editingEmailValue, setEditingEmailValue] = useState("");

  const load = () => {
    UserService.getAll().then(setUsers);
    RoleService.getAll().then(setRoles);
  };

  useEffect(() => { load(); }, []);

  const createUser = async () => {
    setMessage(null);
    if (!fullName || !username || !password || !roleId) {
      setMessage({ text: "All fields are required.", ok: false });
      return;
    }
    try {
      await UserService.create(fullName, username, password, Number(roleId), email);
      setMessage({ text: `User "${username}" created.`, ok: true });
      setFullName(""); setUsername(""); setPassword(""); setEmail(""); setRoleId("");
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not create user.", ok: false });
    }
  };

  const deactivate = async (id: number) => {
    await UserService.deactivate(id);
    load();
  };

  const startEditEmail = (u: UserRecord) => {
    setEditingEmailId(u.id);
    setEditingEmailValue(u.email ?? "");
  };

  const cancelEditEmail = () => {
    setEditingEmailId(null);
    setEditingEmailValue("");
  };

  const saveEmail = async (id: number) => {
    await UserService.updateEmail(id, editingEmailValue || null);
    cancelEditEmail();
    load();
  };

  return (
    <>
      <PageHeader title="Users" subtitle="Create accounts for each role so you can test every part of the app." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New User</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Full Name" value={fullName} onChange={(e) => setFullName(e.target.value)} />
          <TextField size="small" label="Username" value={username} onChange={(e) => setUsername(e.target.value)} />
          <TextField size="small" label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} helperText="Needed for password reset" />
          <TextField size="small" label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} helperText="Min. 8 characters" />
          <Select size="small" displayEmpty value={roleId} onChange={(e) => setRoleId(e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value=""><em>Select role</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <Button variant="contained" onClick={createUser}>Create User</Button>
        </Stack>
      </Paper>

      <SectionTitle>All Users</SectionTitle>
      <Stack spacing={1.25}>
        {users.map((u) => (
          <Paper key={u.id} sx={{ p: 2, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 14 }}>
                {u.fullName} <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>@{u.username}</Typography>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{u.role?.name ?? "No role"}</Typography>

              {editingEmailId === u.id ? (
                <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mt: 0.5 }}>
                  <TextField size="small" type="email" value={editingEmailValue} onChange={(e) => setEditingEmailValue(e.target.value)} placeholder="Email" />
                  <IconButton size="small" color="success" onClick={() => saveEmail(u.id)} title="Save"><CheckIcon fontSize="small" /></IconButton>
                  <IconButton size="small" onClick={cancelEditEmail} title="Cancel"><CloseIcon fontSize="small" /></IconButton>
                </Stack>
              ) : (
                <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mt: 0.5 }}>
                  <Typography sx={{ fontSize: 12, color: u.email ? "text.primary" : "text.secondary", fontStyle: u.email ? "normal" : "italic" }}>
                    {u.email ?? "No email on file"}
                  </Typography>
                  <IconButton size="small" onClick={() => startEditEmail(u)} title="Edit email"><EditIcon sx={{ fontSize: 14 }} /></IconButton>
                </Stack>
              )}
            </Box>
            <Stack direction="row" spacing={1.5} alignItems="center">
              <StatusBadge status={u.isActive ? "Active" : "Inactive"} />
              {u.isActive && (
                <IconButton size="small" color="error" onClick={() => deactivate(u.id)} title="Deactivate">
                  <BlockIcon fontSize="small" />
                </IconButton>
              )}
            </Stack>
          </Paper>
        ))}
      </Stack>
    </>
  );
}
