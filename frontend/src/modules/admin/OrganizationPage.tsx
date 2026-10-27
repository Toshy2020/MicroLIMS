import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { tableHeadSx } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { getErrorMessage } from "../../utils/errorMessage";
import { documentControlService } from "../documentControl/services/documentControlService";
import type { DocumentDepartmentDto, DocumentSectionDto, OrganizationImpactDto } from "../documentControl/types/documentControlTypes";
import { UserService, type UserRecord } from "../users/services/UserService";
import { UserSectionsDialog } from "../users/dialogs/UserSectionsDialog";

// What the one change dialog is doing. Every action states a reason; deactivating something that is still in use
// shows what it would leave behind first and then needs the same reason plus an explicit confirmation.
type Action =
  | { kind: "addDepartment" }
  | { kind: "renameDepartment"; department: DocumentDepartmentDto }
  | { kind: "toggleDepartment"; department: DocumentDepartmentDto }
  | { kind: "addSection"; department: DocumentDepartmentDto }
  | { kind: "renameSection"; section: DocumentSectionDto }
  | { kind: "toggleSection"; section: DocumentSectionDto };

function titleOf(action: Action): string {
  switch (action.kind) {
    case "addDepartment": return "Add department";
    case "renameDepartment": return `Rename department ${action.department.code}`;
    case "toggleDepartment": return `${action.department.isActive ? "Deactivate" : "Activate"} department ${action.department.code}`;
    case "addSection": return `Add section to ${action.department.name}`;
    case "renameSection": return `Rename section ${action.section.name}`;
    case "toggleSection": return `${action.section.isActive ? "Deactivate" : "Activate"} section ${action.section.name}`;
  }
}

function ImpactWarning({ impact, subject }: { impact: OrganizationImpactDto; subject: string }) {
  return (
    <Alert severity="warning">
      {subject} is still in use: <strong>{impact.activeDocuments}</strong> active document(s),{" "}
      <strong>{impact.activeMemberships}</strong> active member(s) and <strong>{impact.openWork}</strong> open work item(s).
      Deactivating it does not delete or move any of them, and it can be activated again. It can never be deleted while it is referenced.
    </Alert>
  );
}

function ChangeDialog({ action, onClose, onDone }: { action: Action | null; onClose: () => void; onDone: () => void }) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [reason, setReason] = useState("");
  const [impact, setImpact] = useState<OrganizationImpactDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setCode("");
    setReason("");
    setError(null);
    setImpact(null);
    if (!action) return;
    setName(action.kind === "renameDepartment" ? action.department.name : action.kind === "renameSection" ? action.section.name : "");
    // Deactivation: look at what is still using it before asking for confirmation.
    let live = true;
    if (action.kind === "toggleDepartment" && action.department.isActive) {
      documentControlService.getDepartmentImpact(action.department.id).then((i) => { if (live) setImpact(i); }).catch((e) => { if (live) setError(getErrorMessage(e, "Could not check what uses this department.")); });
    } else if (action.kind === "toggleSection" && action.section.isActive) {
      documentControlService.getSectionImpact(action.section.id).then((i) => { if (live) setImpact(i); }).catch((e) => { if (live) setError(getErrorMessage(e, "Could not check what uses this section.")); });
    }
    return () => { live = false; };
  }, [action]);

  if (!action) return null;

  const isDeactivation = (action.kind === "toggleDepartment" && action.department.isActive) || (action.kind === "toggleSection" && action.section.isActive);
  const needsName = action.kind === "addDepartment" || action.kind === "renameDepartment" || action.kind === "addSection" || action.kind === "renameSection";
  const needsCode = action.kind === "addDepartment";
  const ready = reason.trim().length > 0
    && (!needsName || name.trim().length > 0)
    && (!needsCode || code.trim().length > 0)
    && (!isDeactivation || impact !== null);

  const save = async () => {
    setSaving(true);
    setError(null);
    const trimmed = reason.trim();
    const confirmDeactivation = impact?.hasReferences === true;
    try {
      switch (action.kind) {
        case "addDepartment":
          await documentControlService.createDepartment({ code: code.trim(), name: name.trim(), reason: trimmed });
          break;
        case "renameDepartment":
          await documentControlService.updateDepartment(action.department.id, { name: name.trim(), isActive: action.department.isActive, reason: trimmed }, action.department.version);
          break;
        case "toggleDepartment":
          await documentControlService.updateDepartment(action.department.id, { name: action.department.name, isActive: !action.department.isActive, reason: trimmed, confirmDeactivation }, action.department.version);
          break;
        case "addSection":
          await documentControlService.createSection({ departmentId: action.department.id, name: name.trim(), reason: trimmed });
          break;
        case "renameSection":
          await documentControlService.updateSection(action.section.id, { name: name.trim(), isActive: action.section.isActive, reason: trimmed }, action.section.version);
          break;
        case "toggleSection":
          await documentControlService.updateSection(action.section.id, { name: action.section.name, isActive: !action.section.isActive, reason: trimmed, confirmDeactivation }, action.section.version);
          break;
      }
      onDone();
    } catch (e) {
      setError(getErrorMessage(e, "The change could not be saved."));
    } finally {
      setSaving(false);
    }
  };

  const subject = action.kind === "toggleDepartment" ? `The department ${action.department.name}` : action.kind === "toggleSection" ? `The section ${action.section.name}` : "";

  return (
    <Dialog open onClose={saving ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{titleOf(action)}</DialogTitle>
      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {isDeactivation && impact?.hasReferences && <ImpactWarning impact={impact} subject={subject} />}
        {isDeactivation && impact && !impact.hasReferences && <Alert severity="info">Nothing refers to it at the moment.</Alert>}
        {needsCode && <TextField label="Code" size="small" required fullWidth value={code} onChange={(e) => setCode(e.target.value)} />}
        {needsName && <TextField label="Name" size="small" required fullWidth value={name} onChange={(e) => setName(e.target.value)} />}
        <TextField
          label="Reason for change"
          size="small"
          multiline
          rows={2}
          required
          fullWidth
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Mandatory administrative justification"
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving} color="inherit">Cancel</Button>
        <Button
          variant="contained"
          color={isDeactivation ? "warning" : "primary"}
          onClick={save}
          disabled={!ready || saving}
        >
          {saving ? "Saving..." : isDeactivation && impact?.hasReferences ? "Deactivate anyway" : isDeactivation ? "Deactivate" : "Save"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function ActiveChip({ active }: { active: boolean }) {
  return <Chip size="small" color={active ? "success" : "default"} variant={active ? "filled" : "outlined"} label={active ? "Active" : "Inactive"} />;
}

// Administration > Organization: the departments and sections that scope documents and laboratory data, and
// which users belong to them. The same endpoints Document Control's configuration used; the editors moved here.
export function OrganizationPage() {
  const { permissions } = useAuth();
  const canManageUsers = permissions.includes(PERMISSIONS.USERS_MANAGE);

  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<Action | null>(null);

  const [users, setUsers] = useState<UserRecord[]>([]);
  const [userFilter, setUserFilter] = useState("");
  const [membershipUser, setMembershipUser] = useState<UserRecord | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      setDepartments(await documentControlService.getDepartments(true));
    } catch (e) {
      setError(getErrorMessage(e, "Could not load departments."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!canManageUsers) return;
    let live = true;
    UserService.getAll("active").then((list) => { if (live) setUsers(list); }).catch(() => { if (live) setUsers([]); });
    return () => { live = false; };
  }, [canManageUsers]);

  const visibleUsers = useMemo(() => {
    const q = userFilter.trim().toLowerCase();
    return q ? users.filter((u) => u.fullName.toLowerCase().includes(q) || u.username.toLowerCase().includes(q)) : users;
  }, [users, userFilter]);

  return (
    <Box>
      <PageHeader
        title="Organization"
        subtitle="Departments and sections own documents and decide what laboratory data each user sees. Every change asks for a reason; one that is still in use is deactivated, never deleted."
      >
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAction({ kind: "addDepartment" })}>Add department</Button>
      </PageHeader>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}><CircularProgress aria-label="Loading" /></Box>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 4 }}>
          {departments.map((d) => (
            <Paper key={d.id} variant="outlined" sx={{ p: 2, opacity: d.isActive ? 1 : 0.7 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap", mb: 1 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>{d.code}: {d.name}</Typography>
                <ActiveChip active={d.isActive} />
                <Box sx={{ flexGrow: 1 }} />
                <Button size="small" onClick={() => setAction({ kind: "addSection", department: d })}>Add section</Button>
                <Button size="small" aria-label={`Rename department ${d.code}`} onClick={() => setAction({ kind: "renameDepartment", department: d })}>Rename</Button>
                <Button
                  size="small"
                  color={d.isActive ? "warning" : "success"}
                  aria-label={`${d.isActive ? "Deactivate" : "Activate"} department ${d.code}`}
                  onClick={() => setAction({ kind: "toggleDepartment", department: d })}
                >
                  {d.isActive ? "Deactivate" : "Activate"}
                </Button>
              </Box>
              <TableContainer>
                <Table size="small" aria-label={`Sections of ${d.name}`}>
                  <TableHead sx={tableHeadSx}>
                    <TableRow>
                      <TableCell>Section</TableCell>
                      <TableCell>Status</TableCell>
                      <TableCell align="right">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {d.sections.map((s) => (
                      <TableRow key={s.id} sx={{ opacity: s.isActive ? 1 : 0.7 }}>
                        <TableCell>{s.name}</TableCell>
                        <TableCell><ActiveChip active={s.isActive} /></TableCell>
                        <TableCell align="right">
                          <Button size="small" aria-label={`Rename section ${s.name}`} onClick={() => setAction({ kind: "renameSection", section: s })}>Rename</Button>
                          <Button
                            size="small"
                            color={s.isActive ? "warning" : "success"}
                            aria-label={`${s.isActive ? "Deactivate" : "Activate"} section ${s.name}`}
                            onClick={() => setAction({ kind: "toggleSection", section: s })}
                          >
                            {s.isActive ? "Deactivate" : "Activate"}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                    {d.sections.length === 0 && (
                      <TableRow><TableCell colSpan={3} align="center" sx={{ color: "text.secondary" }}>No sections registered under this department.</TableCell></TableRow>
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          ))}
          {departments.length === 0 && <Alert severity="info">No departments are configured yet.</Alert>}
        </Box>
      )}

      <Typography variant="h6" sx={{ mb: 1 }}>Memberships</Typography>
      {canManageUsers ? (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <TextField
            size="small"
            label="Find a user"
            value={userFilter}
            onChange={(e) => setUserFilter(e.target.value)}
            sx={{ mb: 2, minWidth: 260 }}
          />
          <TableContainer>
            <Table size="small" aria-label="User memberships">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell>User</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell align="right">Departments and sections</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {visibleUsers.map((u) => (
                  <TableRow key={u.id}>
                    <TableCell>{u.fullName} <Typography component="span" variant="caption" color="text.secondary">@{u.username}</Typography></TableCell>
                    <TableCell>{u.role?.name ?? ""}</TableCell>
                    <TableCell align="right">
                      <Button size="small" aria-label={`Edit memberships of ${u.username}`} onClick={() => setMembershipUser(u)}>Edit memberships</Button>
                    </TableCell>
                  </TableRow>
                ))}
                {visibleUsers.length === 0 && (
                  <TableRow><TableCell colSpan={3} align="center" sx={{ color: "text.secondary" }}>No users found.</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      ) : (
        <Alert severity="info">Editing who belongs to which department needs the Users.Manage permission.</Alert>
      )}

      <ChangeDialog
        action={action}
        onClose={() => setAction(null)}
        onDone={() => { setAction(null); toast.success("Saved."); load(); }}
      />
      <UserSectionsDialog
        open={!!membershipUser}
        user={membershipUser}
        onClose={() => setMembershipUser(null)}
        onSuccess={(message) => toast.success(message)}
      />
    </Box>
  );
}
