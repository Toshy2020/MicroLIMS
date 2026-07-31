import { useEffect, useState } from "react";
import { Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, Box, IconButton } from "@mui/material";
import BlockIcon from "@mui/icons-material/Block";
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
  const [roleId, setRoleId] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

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
      await UserService.create(fullName, username, password, Number(roleId));
      setMessage({ text: `User "${username}" created.`, ok: true });
      setFullName(""); setUsername(""); setPassword(""); setRoleId("");
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not create user.", ok: false });
    }
  };

  const deactivate = async (id: number) => {
    await UserService.deactivate(id);
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
