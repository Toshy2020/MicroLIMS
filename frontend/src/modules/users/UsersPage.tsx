import { useEffect, useState } from "react";
import {
  Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, Box,
  Chip, Tooltip, Table,
  TableBody, TableCell, TableContainer, TableHead, TableRow, IconButton
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { toast } from "sonner";
import { tableHeadSx } from "../../theme";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import EditIcon from "@mui/icons-material/Edit";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import LockResetIcon from "@mui/icons-material/LockReset";
import SecurityIcon from "@mui/icons-material/Security";
import BlockIcon from "@mui/icons-material/Block";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";
import KeyIcon from "@mui/icons-material/Key";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DeleteForeverIcon from "@mui/icons-material/DeleteForever";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import AccountTreeIcon from "@mui/icons-material/AccountTree";

import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService, RoleRecord } from "../roles/services/RoleService";
import { useAuth } from "../../contexts/AuthContext";
import { useLoadFailures } from "../../hooks/useLoadFailures";
import { UserSectionsDialog } from "./dialogs/UserSectionsDialog";
import { UserSessionsDialog } from "./dialogs/UserSessionsDialog";
import DevicesIcon from "@mui/icons-material/Devices";
import { LoadFailuresAlert } from "../../components/LoadErrorAlert";
import { MetaBadge, ToneBadge } from "../../components/StatusBadge";

export function UsersPage() {
  const theme = useTheme();
  const { userId: currentUserId } = useAuth();
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);

  // New user form state
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [roleId, setRoleId] = useState("");
  const [createReason, setCreateReason] = useState("");

  // Dialog state variables
  const [editProfileUser, setEditProfileUser] = useState<UserRecord | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editUsername, setEditUsername] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editJobTitle, setEditJobTitle] = useState("");
  const [editReason, setEditReason] = useState("");

  const [roleDialogUser, setRoleDialogUser] = useState<UserRecord | null>(null);
  const [newRoleId, setNewRoleId] = useState<number | "">("");
  const [roleReason, setRoleReason] = useState("");

  const [statusDialogUser, setStatusDialogUser] = useState<UserRecord | null>(null);
  const [statusReason, setStatusReason] = useState("");

  const [unlockDialogUser, setUnlockDialogUser] = useState<UserRecord | null>(null);
  const [unlockReason, setUnlockReason] = useState("");

  const [resetDialogUser, setResetDialogUser] = useState<UserRecord | null>(null);
  const [resetReason, setResetReason] = useState("");

  // Admin-Assisted Recovery state
  const [adminRecoveryUser, setAdminRecoveryUser] = useState<UserRecord | null>(null);
  const [adminRecoveryReason, setAdminRecoveryReason] = useState("");
  const [generatedCode, setGeneratedCode] = useState<string | null>(null);
  const [codeCopied, setCodeCopied] = useState(false);

  const [forcePwdUser, setForcePwdUser] = useState<UserRecord | null>(null);
  const [forcePwdReason, setForcePwdReason] = useState("");

  const [deleteDialogUser, setDeleteDialogUser] = useState<UserRecord | null>(null);
  const [deleteReason, setDeleteReason] = useState("");
  // Role change and permanent delete are electronically signed (password re-entry).
  const [signing, setSigning] = useState<"role" | "delete" | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [sectionsUser, setSectionsUser] = useState<UserRecord | null>(null);
  const [sessionsUser, setSessionsUser] = useState<UserRecord | null>(null);

  // A failed load used to leave the user table (or the role picker) empty
  // with no explanation.
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();
  const load = () => {
    resetFailures();
    UserService.getAll().then(setUsers).catch(fail("users"));
    RoleService.getAll().then(setRoles).catch(fail("roles"));
  };

  // Mount-only: load is redefined each render and only runs set-state calls.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { load(); }, []);

  const handleCreateUser = async () => {
    if (!fullName || !username || !password || !roleId || !createReason.trim()) {
      toast.error("Full Name, Username, Password, Role, and a Reason are required.");
      return;
    }
    try {
      await UserService.create(fullName, username, password, Number(roleId), createReason.trim(), email, jobTitle);
      toast.success(`User "${username}" created successfully.`);
      setFullName(""); setUsername(""); setPassword(""); setEmail(""); setJobTitle(""); setRoleId(""); setCreateReason("");
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not create user.");
    }
  };

  // Profile Edit
  const openEditProfile = (u: UserRecord) => {
    setEditProfileUser(u);
    setEditFullName(u.fullName);
    setEditUsername(u.username);
    setEditEmail(u.email ?? "");
    setEditJobTitle(u.jobTitle ?? "");
    setEditReason("");
  };

  const handleSaveProfile = async () => {
    if (!editProfileUser) return;
    if (!editReason.trim()) {
      toast.error("A reason is required to change a user's profile.");
      return;
    }
    try {
      await UserService.updateProfile(editProfileUser.id, editFullName, editUsername, editEmail || null, editJobTitle || null, editReason.trim(), editProfileUser.version);
      toast.success(`Profile updated for ${editUsername}.`);
      setEditProfileUser(null);
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not update profile.");
    }
  };

  // Role Change
  const openChangeRole = (u: UserRecord) => {
    setRoleDialogUser(u);
    setNewRoleId(u.roleId);
    setRoleReason("");
  };

  // Opens the signature step; the change is sent only once the password is entered.
  const handleSaveRole = () => {
    if (!roleDialogUser || !newRoleId || !roleReason.trim()) {
      toast.error("Selected Role and a Reason are required for role change.");
      return;
    }
    setSigning("role");
  };

  // A failure is thrown back to the signature dialog, which shows it and stays open.
  const confirmRoleSignature = async (signaturePassword: string) => {
    if (!roleDialogUser || !newRoleId) return;
    await UserService.changeRole(roleDialogUser.id, Number(newRoleId), roleReason.trim(), signaturePassword);
    toast.success(`Role changed for ${roleDialogUser.username}.`);
    setSigning(null);
    setRoleDialogUser(null);
    load();
  };

  // Enable/Disable
  const openStatusDialog = (u: UserRecord) => {
    setStatusDialogUser(u);
    setStatusReason("");
  };

  const handleSaveStatus = async () => {
    if (!statusDialogUser) return;
    const newStatus = !statusDialogUser.isActive;
    if (!statusReason.trim()) {
      toast.error(`A reason is required to ${newStatus ? "enable" : "disable"} a user account.`);
      return;
    }
    try {
      await UserService.setStatus(statusDialogUser.id, newStatus, statusReason.trim());
      toast.success(`Account ${newStatus ? "enabled" : "disabled"} for ${statusDialogUser.username}.`);
      setStatusDialogUser(null);
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not update account status.");
    }
  };

  // Unlock
  const openUnlockDialog = (u: UserRecord) => {
    setUnlockDialogUser(u);
    setUnlockReason("");
  };

  const handleSaveUnlock = async () => {
    if (!unlockDialogUser) return;
    if (!unlockReason.trim()) {
      toast.error("A reason is required to unlock a user account.");
      return;
    }
    try {
      await UserService.unlock(unlockDialogUser.id, unlockReason.trim());
      toast.success(`Account unlocked for ${unlockDialogUser.username}.`);
      setUnlockDialogUser(null);
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not unlock account.");
    }
  };

  // Standard Email Password Reset
  const openResetDialog = (u: UserRecord) => {
    setResetDialogUser(u);
    setResetReason("");
  };

  const handleSavePasswordReset = async () => {
    if (!resetDialogUser) return;
    if (!resetReason.trim()) {
      toast.error("A reason is required to reset a user's password.");
      return;
    }
    try {
      await UserService.initiatePasswordReset(resetDialogUser.id, resetReason.trim());
      toast.success(`Password reset instructions initiated for ${resetDialogUser.username}.`);
      setResetDialogUser(null);
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not initiate password reset.");
    }
  };

  // Admin-Assisted Recovery
  const openAdminRecoveryDialog = (u: UserRecord) => {
    setAdminRecoveryUser(u);
    setAdminRecoveryReason("");
    setGeneratedCode(null);
    setCodeCopied(false);
  };

  const handleGenerateRecoveryCode = async () => {
    if (!adminRecoveryUser || !adminRecoveryReason) {
      toast.error("A reason is required for admin-assisted password recovery.");
      return;
    }
    try {
      const result = await UserService.adminPasswordRecovery(adminRecoveryUser.id, adminRecoveryReason);
      setGeneratedCode(result.recoveryCode);
      toast.success(`Recovery code generated for ${adminRecoveryUser.username}.`);
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not generate recovery code.");
    }
  };

  const handleCopyCode = () => {
    if (generatedCode) {
      navigator.clipboard.writeText(generatedCode);
      setCodeCopied(true);
      toast.success("Recovery code copied to clipboard.");
      setTimeout(() => setCodeCopied(false), 3000);
    }
  };

  const closeAdminRecoveryDialog = () => {
    setAdminRecoveryUser(null);
    setAdminRecoveryReason("");
    setGeneratedCode(null);
    setCodeCopied(false);
  };

  // Force Password Change
  const openForcePwdDialog = (u: UserRecord) => {
    setForcePwdUser(u);
    setForcePwdReason("");
  };

  const handleSaveForcePwd = async () => {
    if (!forcePwdUser) return;
    if (!forcePwdReason.trim()) {
      toast.error("A reason is required to force a password change.");
      return;
    }
    try {
      await UserService.forcePasswordChange(forcePwdUser.id, forcePwdReason.trim());
      toast.success(`Forced password change set for ${forcePwdUser.username}.`);
      setForcePwdUser(null);
      load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? "Could not force password change.");
    }
  };

  // Permanent Delete
  const openDeleteDialog = (u: UserRecord) => {
    setDeleteDialogUser(u);
    setDeleteError(null);
    setDeleteReason("");
  };

  const closeDeleteDialog = () => {
    if (deleting) return;
    setDeleteDialogUser(null);
    setDeleteError(null);
  };

  const handleHardDelete = () => {
    if (!deleteDialogUser) return;
    if (!deleteReason.trim()) {
      setDeleteError("A reason is required to permanently delete a user.");
      return;
    }
    setDeleteError(null);
    setSigning("delete");
  };

  // A refused password stays in the signature dialog; a business refusal
  // (the user has history) closes it and is shown in the delete dialog.
  const confirmDeleteSignature = async (signaturePassword: string) => {
    if (!deleteDialogUser) return;
    setDeleting(true);
    try {
      await UserService.hardDelete(deleteDialogUser.id, deleteReason.trim(), signaturePassword);
      toast.success(`User "${deleteDialogUser.username}" was permanently deleted.`);
      setSigning(null);
      setDeleteDialogUser(null);
      load();
    } finally {
      setDeleting(false);
    }
  };

  return (
    <>
      <PageHeader title="User Management" subtitle="Manage system users, role assignments, security status, and account access." />
      <LoadFailuresAlert failed={failedLists} retryHint="Reload the page to try again." sx={{ mb: 2 }} />
      <SectionTitle>Create New User</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box
          component="form"
          onSubmit={(e) => {
            e.preventDefault();
            handleCreateUser();
          }}
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: "1fr 1fr",
              md: "repeat(3, 1fr)",
              lg: "1.2fr 1fr 1.2fr 1fr 1.2fr 1.2fr 1.2fr auto"
            },
            gap: 2,
            alignItems: "start"
          }}
        >
          <TextField
            size="small"
            label="Full Name"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            required
            fullWidth
          />
          <TextField
            size="small"
            label="Username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
            fullWidth
          />
          <TextField
            size="small"
            label="Email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            helperText="Password reset destination"
            fullWidth
          />
          <TextField
            size="small"
            label="Job Title"
            value={jobTitle}
            onChange={(e) => setJobTitle(e.target.value)}
            fullWidth
          />
          <TextField
            size="small"
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            helperText="Min 8 chars"
            required
            fullWidth
          />
          <Select
            size="small"
            displayEmpty
            value={roleId}
            onChange={(e) => setRoleId(e.target.value)}
            fullWidth
            inputProps={{ "aria-label": "Role" }}
          >
            <MenuItem value=""><em>Select Role *</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <TextField
            size="small"
            label="Reason"
            value={createReason}
            onChange={(e) => setCreateReason(e.target.value)}
            helperText="Why this account is needed"
            required
            fullWidth
          />
          <Button
            type="submit"
            variant="contained"
            color="primary"
            sx={{ height: 40, whiteSpace: "nowrap" }}
          >
            Create User
          </Button>
        </Box>
      </Paper>

      <SectionTitle>All System Users</SectionTitle>
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell>User</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Role</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Security / Password</TableCell>
              <TableCell>Last Login</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => {
              const isSelf = u.id === currentUserId;
              return (
                <TableRow key={u.id} hover>
                  <TableCell>
                    <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{u.fullName}</Typography>
                    <Typography sx={{ color: "text.secondary", fontSize: 12 }}>@{u.username} {isSelf && <Chip label="You" size="small" color="primary" variant="outlined" sx={{ height: 20, fontSize: 12 }} />}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 13, fontStyle: u.email ? "normal" : "italic", color: u.email ? "text.primary" : "text.secondary" }}>
                      {u.email ?? "No email"}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <MetaBadge label={u.role?.name ?? "No role"} />
                  </TableCell>
                  <TableCell>
                    {u.isActive ? (
                      <ToneBadge tone="notDetected" label="Active" />
                    ) : (
                      <ToneBadge tone="detected" label="Disabled" />
                    )}
                  </TableCell>
                  <TableCell>
                    <Stack direction="column" spacing={0.5}>
                      {u.isLocked && <Box><ToneBadge tone="action" label="Account Locked" /></Box>}
                      {u.mustChangePassword && <Box><ToneBadge tone="info" label="Must Change Password" /></Box>}
                      {!u.isLocked && !u.mustChangePassword && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Normal</Typography>}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{
                      justifyContent: "flex-end"
                    }}>
                      <Tooltip title="Edit Profile">
                        <IconButton size="small" onClick={() => openEditProfile(u)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title="Laboratory Sections">
                        <IconButton size="small" color="info" onClick={() => setSectionsUser(u)}>
                          <AccountTreeIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      <Tooltip title="Sessions">
                        <IconButton aria-label="Sessions" size="small" onClick={() => setSessionsUser(u)}>
                          <DevicesIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? "System Administrators cannot change their own role" : "Change Role"}>
                        <span>
                          <IconButton aria-label={isSelf ? "System Administrators cannot change their own role" : "Change Role"} size="small" color="primary" disabled={isSelf} onClick={() => openChangeRole(u)}>
                            <PersonOutlineIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title={isSelf && u.isActive ? "System Administrators cannot disable their own account" : (u.isActive ? "Disable Account" : "Enable Account")}>
                        <span>
                          <IconButton aria-label={isSelf && u.isActive ? "System Administrators cannot disable their own account" : (u.isActive ? "Disable Account" : "Enable Account")} size="small" color={u.isActive ? "error" : "success"} disabled={isSelf && u.isActive} onClick={() => openStatusDialog(u)}>
                            <BlockIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      {u.isLocked && (
                        <Tooltip title="Unlock Account">
                          <IconButton size="small" color="warning" onClick={() => openUnlockDialog(u)}><LockOpenIcon fontSize="small" /></IconButton>
                        </Tooltip>
                      )}

                      <Tooltip title="Reset Password via Email">
                        <IconButton size="small" color="secondary" onClick={() => openResetDialog(u)}><LockResetIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? "Cannot use admin recovery on own account" : (!u.isActive ? "Enable user first to perform recovery" : "Admin-Assisted Password Recovery")}>
                        <span>
                          <IconButton aria-label={isSelf ? "Cannot use admin recovery on own account" : (!u.isActive ? "Enable user first to perform recovery" : "Admin-Assisted Password Recovery")} size="small" color="warning" disabled={isSelf || !u.isActive} onClick={() => openAdminRecoveryDialog(u)}>
                            <KeyIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title="Force Password Change at Next Login">
                        <IconButton size="small" color="info" onClick={() => openForcePwdDialog(u)}><SecurityIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? "You cannot permanently delete your own account" : "Permanently Delete User"}>
                        <span>
                          <IconButton aria-label={isSelf ? "You cannot permanently delete your own account" : "Permanently Delete User"} size="small" color="error" disabled={isSelf} onClick={() => openDeleteDialog(u)}>
                            <DeleteForeverIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                    </Stack>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Edit Profile Dialog */}
      <FloatingDialog
        open={Boolean(editProfileUser)}
        onClose={() => setEditProfileUser(null)}
        maxWidth="xs"
        title={`Edit Profile: ${editProfileUser?.username}`}
        actions={
          <>
            <Button onClick={() => setEditProfileUser(null)}>Cancel</Button>
            <Button variant="contained" onClick={handleSaveProfile} disabled={!editReason.trim()}>Save Profile</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Full Name" size="small" value={editFullName} onChange={(e) => setEditFullName(e.target.value)} fullWidth />
          <TextField label="Username" size="small" value={editUsername} onChange={(e) => setEditUsername(e.target.value)} fullWidth />
          <TextField label="Email" size="small" type="email" value={editEmail} onChange={(e) => setEditEmail(e.target.value)} fullWidth helperText="Required for email password resets" />
          <TextField label="Job Title" size="small" value={editJobTitle} onChange={(e) => setEditJobTitle(e.target.value)} fullWidth />
          <TextField label="Reason for Change" size="small" multiline rows={2} value={editReason} onChange={(e) => setEditReason(e.target.value)} required fullWidth placeholder="Mandatory administrative justification" />
        </Stack>
      </FloatingDialog>

      {/* Change Role Dialog */}
      <FloatingDialog
        open={Boolean(roleDialogUser)}
        onClose={() => setRoleDialogUser(null)}
        maxWidth="xs"
        title={`Change Role: ${roleDialogUser?.username}`}
        actions={
          <>
            <Button onClick={() => setRoleDialogUser(null)}>Cancel</Button>
            <Button variant="contained" color="primary" onClick={handleSaveRole} disabled={!newRoleId || !roleReason.trim()}>Change Role</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">Current Role: <strong>{roleDialogUser?.role?.name ?? "None"}</strong></Typography>
          <Select size="small" value={newRoleId} onChange={(e) => setNewRoleId(Number(e.target.value))} fullWidth displayEmpty inputProps={{ "aria-label": "New Role" }}>
            <MenuItem value=""><em>Select New Role</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <TextField label="Reason for Change" size="small" multiline rows={2} value={roleReason} onChange={(e) => setRoleReason(e.target.value)} required fullWidth placeholder="Mandatory administrative justification" />
        </Stack>
      </FloatingDialog>

      {/* Enable / Disable Status Dialog */}
      <FloatingDialog
        open={Boolean(statusDialogUser)}
        onClose={() => setStatusDialogUser(null)}
        maxWidth="xs"
        title={statusDialogUser?.isActive ? "Disable User Account" : "Enable User Account"}
        actions={
          <>
            <Button onClick={() => setStatusDialogUser(null)}>Cancel</Button>
            <Button variant="contained" color={statusDialogUser?.isActive ? "error" : "success"} onClick={handleSaveStatus} disabled={!statusReason.trim()}>
              {statusDialogUser?.isActive ? "Disable Account" : "Enable Account"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            Are you sure you want to {statusDialogUser?.isActive ? "disable" : "enable"} account <strong>{statusDialogUser?.fullName} (@{statusDialogUser?.username})</strong>?
          </Typography>
          <TextField label={statusDialogUser?.isActive ? "Reason for Disabling" : "Reason for Enabling"} size="small" multiline rows={2} value={statusReason} onChange={(e) => setStatusReason(e.target.value)} required fullWidth placeholder="Mandatory justification" />
        </Stack>
      </FloatingDialog>

      {/* Unlock Dialog */}
      <FloatingDialog
        open={Boolean(unlockDialogUser)}
        onClose={() => setUnlockDialogUser(null)}
        maxWidth="xs"
        title="Unlock User Account"
        actions={
          <>
            <Button onClick={() => setUnlockDialogUser(null)}>Cancel</Button>
            <Button variant="contained" color="warning" onClick={handleSaveUnlock} disabled={!unlockReason.trim()}>Unlock Account</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            Unlock account for <strong>{unlockDialogUser?.fullName} (@{unlockDialogUser?.username})</strong>? This will clear failed login attempts and reset account lock.
          </Typography>
          <TextField label="Reason for Unlock" size="small" value={unlockReason} onChange={(e) => setUnlockReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Standard Email Reset Dialog */}
      <FloatingDialog
        open={Boolean(resetDialogUser)}
        onClose={() => setResetDialogUser(null)}
        maxWidth="xs"
        title="Initiate Email Password Reset"
        actions={
          <>
            <Button onClick={() => setResetDialogUser(null)}>Cancel</Button>
            <Button variant="contained" color="secondary" onClick={handleSavePasswordReset} disabled={!resetReason.trim()}>Send Reset Link</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            Send password reset instructions to <strong>{resetDialogUser?.fullName} ({resetDialogUser?.email ?? "No email on file"})</strong>?
          </Typography>
          <Alert severity="info" sx={{ fontSize: 12 }}>
            The system will generate a secure reset token link and send it via email. Plaintext passwords are never generated or shown to administrators.
          </Alert>
          <TextField label="Reason for Reset" size="small" value={resetReason} onChange={(e) => setResetReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Admin-Assisted Password Recovery Dialog */}
      <FloatingDialog
        open={Boolean(adminRecoveryUser)}
        onClose={closeAdminRecoveryDialog}
        maxWidth="sm"
        title="Administrator-Assisted Password Recovery"
        actions={
          !generatedCode ? (
            <>
              <Button onClick={closeAdminRecoveryDialog}>Cancel</Button>
              <Button
                variant="contained"
                color="warning"
                onClick={handleGenerateRecoveryCode}
                disabled={!adminRecoveryReason}
              >
                Generate Recovery Code
              </Button>
            </>
          ) : (
            <Button variant="contained" onClick={closeAdminRecoveryDialog}>
              Close & Done
            </Button>
          )
        }
      >
          {!generatedCode ? (
            <Stack spacing= {2} sx={{ mt: 1 }}>
              <Typography variant="body2">
                Initiate administrator-assisted password recovery for user <strong>{adminRecoveryUser?.fullName} (@{adminRecoveryUser?.username})</strong>.
              </Typography>
              <Alert severity="warning" sx={{ fontSize: 12 }}>
                This will generate a one-time 15-minute recovery code. The code will be displayed <strong>ONCE</strong> on this screen and must be communicated to the user via approved internal channels. Plaintext codes are never stored in the database.
              </Alert>
              <TextField
                label="Reason for Recovery (Mandatory)"
                size="small"
                multiline
                rows={2}
                value={adminRecoveryReason}
                onChange={(e) => setAdminRecoveryReason(e.target.value)}
                required
                fullWidth
                placeholder="e.g. User forgot password and cannot access registered email."
              />
            </Stack>
          ) : (
            <Stack spacing={2.5} sx={{ mt: 1, alignItems: "center", textAlign: "center" }}>
              <Alert severity="success" sx={{ width: "100%" }}>
                One-time recovery code generated successfully!
              </Alert>
              <Typography variant="subtitle2" sx={{
                color: "text.secondary"
              }}>
                Provide this recovery code to <strong>{adminRecoveryUser?.fullName}</strong>:
              </Typography>
              <Box
                sx={{
                  p: 2.5,
                  bgcolor: theme.palette.mode === "dark" ? "action.hover" : "grey.100",
                  borderRadius: 2,
                  border: "2px dashed",
                  borderColor: "warning.main",
                  width: "100%",
                }}
              >
                <Typography
                  sx={{
                    fontFamily: "monospace",
                    fontSize: 28,
                    fontWeight: 700,
                    letterSpacing: 3,
                    color: "primary.main",
                  }}
                >
                  {generatedCode}
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} sx={{
                alignItems: "center"
              }}>
                <Button
                  variant="outlined"
                  color="primary"
                  startIcon={<ContentCopyIcon />}
                  onClick={handleCopyCode}
                >
                  {codeCopied ? "Copied!" : "Copy Code"}
                </Button>
                <Chip label="Expires in 15 minutes" color="warning" size="small" />
              </Stack>
              <Alert severity="error" sx={{ fontSize: 12, textAlign: "left", width: "100%" }}>
                <strong>IMPORTANT:</strong> This code will NOT be displayed again after closing this dialog. Ensure the code is transmitted securely to the user.
              </Alert>
            </Stack>
          )}
      </FloatingDialog>

      {/* Force Password Change Dialog */}
      <FloatingDialog
        open={Boolean(forcePwdUser)}
        onClose={() => setForcePwdUser(null)}
        maxWidth="xs"
        title="Force Password Change"
        actions={
          <>
            <Button onClick={() => setForcePwdUser(null)}>Cancel</Button>
            <Button variant="contained" color="info" onClick={handleSaveForcePwd} disabled={!forcePwdReason.trim()}>Force Password Change</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            Require <strong>{forcePwdUser?.fullName} (@{forcePwdUser?.username})</strong> to change their password on next login?
          </Typography>
          <TextField label="Reason" size="small" value={forcePwdReason} onChange={(e) => setForcePwdReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Permanent Delete Dialog */}
      <FloatingDialog
        open={Boolean(deleteDialogUser)}
        onClose={closeDeleteDialog}
        maxWidth="xs"
        title="Permanently Delete User"
        actions={
          <>
            <Button onClick={closeDeleteDialog} disabled={deleting}>Cancel</Button>
            <Button variant="contained" color="error" onClick={handleHardDelete} disabled={deleting || !deleteReason.trim()} startIcon={<DeleteForeverIcon />}>
              {deleting ? "Deleting..." : "Permanently Delete"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          {deleteError && <Alert severity="error">{deleteError}</Alert>}
          <Alert severity="warning" icon={<WarningAmberIcon />}>
            This permanently removes the account and cannot be undone. It is only possible when the user has no activity history anywhere in the system. If this fails, deactivate the account instead.
          </Alert>
          <Typography variant="body2">
            Permanently delete <strong>{deleteDialogUser?.fullName} (@{deleteDialogUser?.username})</strong>? You will be asked to sign with your password.
          </Typography>
          <TextField label="Reason for Deletion" size="small" multiline rows={2} value={deleteReason} onChange={(e) => setDeleteReason(e.target.value)} required fullWidth placeholder="Mandatory justification" />
        </Stack>
      </FloatingDialog>

      {/* Electronic signature for the role change and the permanent delete */}
      <SignatureDialog
        open={signing === "role"}
        meaningStatement={`I am changing the role of ${roleDialogUser?.username ?? "this user"} to ${roles.find((r) => r.id === newRoleId)?.name ?? "the selected role"}. Reason: ${roleReason.trim()}`}
        onCancel={() => setSigning(null)}
        onConfirm={confirmRoleSignature}
      />
      <SignatureDialog
        open={signing === "delete"}
        meaningStatement={`I am permanently deleting the account ${deleteDialogUser?.username ?? ""}. Reason: ${deleteReason.trim()}`}
        onCancel={() => setSigning(null)}
        onConfirm={async (pw) => {
          try {
            await confirmDeleteSignature(pw);
          } catch (e: any) {
            // Only a refused password stays in the signature dialog.
            const msg: string = e?.response?.data?.message ?? "Could not delete user.";
            if (/password|signature/i.test(msg)) throw e;
            setSigning(null);
            setDeleteError(msg);
          }
        }}
      />

      {/* Laboratory Sections Dialog */}
      <UserSectionsDialog
        open={Boolean(sectionsUser)}
        onClose={() => setSectionsUser(null)}
        user={sectionsUser}
        onSuccess={(msg) => {
          toast.success(msg);
          setSectionsUser(null);
        }}
      />

      <UserSessionsDialog
        open={Boolean(sessionsUser)}
        onClose={() => setSessionsUser(null)}
        user={sessionsUser}
        onRevoked={(msg) => toast.success(msg)}
      />
    </>
  );
}
