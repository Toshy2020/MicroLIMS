import { useEffect, useMemo, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import {
  Paper, TextField, Button, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Chip, Typography, InputAdornment, Alert
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AddIcon from "@mui/icons-material/Add";
import { PageHeader } from "../../components/PageHeader";
import { RoleService, RoleRecord } from "./services/RoleService";
import { UserService, UserRecord } from "../users/services/UserService";

export function RolesPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [search, setSearch] = useState("");
  const [message, setMessage] = useState<string | null>((location.state as { message?: string } | null)?.message ?? null);

  useEffect(() => {
    RoleService.getAll().then(setRoles).catch(() => {});
    // Reused rather than adding a backend user-count endpoint - UsersPage
    // already fetches the same list for its own table, same
    // SystemAdministrator-only gating as this page.
    UserService.getAll().then(setUsers).catch(() => {});
    if (location.state) navigate(location.pathname, { replace: true, state: null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const userCountByRoleId = useMemo(() => {
    const counts = new Map<number, number>();
    for (const u of users) counts.set(u.roleId, (counts.get(u.roleId) ?? 0) + 1);
    return counts;
  }, [users]);

  const filtered = roles.filter((r) => r.name.toLowerCase().includes(search.trim().toLowerCase()));

  return (
    <>
      <PageHeader title="Roles" subtitle="System Administrator, Section Head, Reviewer, Analyst, and any custom roles." />
      {message && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setMessage(null)}>{message}</Alert>}

      <Paper sx={{ p: 2, mb: 2, display: "flex", justifyContent: "space-between", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
        <TextField
          size="small"
          placeholder="Search by role name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          sx={{ minWidth: 260 }}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }}
        />
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/roles/new")}>
          Create Role
        </Button>
      </Paper>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ backgroundColor: "action.hover" }}>
              <TableCell>Role</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Users</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((r) => (
              <TableRow key={r.id} hover sx={{ cursor: "pointer" }} onClick={() => navigate(`/roles/${r.id}`)}>
                <TableCell>
                  <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{r.name}</Typography>
                  {r.description && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.description}</Typography>}
                </TableCell>
                <TableCell>
                  <Chip
                    label={r.isSystemRole ? "System" : "Custom"}
                    size="small"
                    color={r.isSystemRole ? "secondary" : "default"}
                    variant={r.isSystemRole ? "filled" : "outlined"}
                  />
                </TableCell>
                <TableCell>
                  <Chip label={r.isActive ? "Active" : "Inactive"} size="small" color={r.isActive ? "success" : "default"} />
                </TableCell>
                <TableCell align="right">{userCountByRoleId.get(r.id) ?? 0}</TableCell>
              </TableRow>
            ))}
            {filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={4}>
                  <Typography sx={{ fontSize: 13, color: "text.secondary", textAlign: "center", py: 2 }}>
                    No roles match "{search}".
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </>
  );
}
