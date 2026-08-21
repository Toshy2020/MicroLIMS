import { useEffect, useState } from "react";
import {
  Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, Box,
  Chip, Dialog, DialogTitle, DialogContent, DialogActions, Tooltip, Table,
  TableBody, TableCell, TableContainer, TableHead, TableRow, IconButton
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import LockResetIcon from "@mui/icons-material/LockReset";
import SecurityIcon from "@mui/icons-material/Security";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import PersonOutlineIcon from "@mui/icons-material/PersonOutline";
import KeyIcon from "@mui/icons-material/Key";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";

import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService, RoleRecord } from "../roles/services/RoleService";
import { useAuth } from "../../contexts/AuthContext";

export function UsersPage() {
  const { userId: currentUserId } = useAuth();
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // New user form state
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [roleId, setRoleId] = useState("");

  // Dialog state variables
  const [editProfileUser, setEditProfileUser] = useState<UserRecord | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editUsername, setEditUsername] = useState("");
  const [editEmail, setEditEmail] = useState("");

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

  const load = () => {
    UserService.getAll().then(setUsers).catch(() => {});
    RoleService.getAll().then(setRoles).catch(() => {});
  };

  useEffect(() => { load(); }, []);

  const handleCreateUser = async () => {
    setMessage(null);
    if (!fullName || !username || !password || !roleId) {
      setMessage({ text: "Full Name, Username, Password, and Role are required.", ok: false });
      return;
    }
    try {
      await UserService.create(fullName, username, password, Number(roleId), email);
      setMessage({ text: `User "${username}" created successfully.`, ok: true });
      setFullName(""); setUsername(""); setPassword(""); setEmail(""); setRoleId("");
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not create user.", ok: false });
    }
  };

  // Profile Edit
  const openEditProfile = (u: UserRecord) => {
    setEditProfileUser(u);
    setEditFullName(u.fullName);
    setEditUsername(u.username);
    setEditEmail(u.email ?? "");
  };

  const handleSaveProfile = async () => {
    if (!editProfileUser) return;
    try {
      await UserService.updateProfile(editProfileUser.id, editFullName, editUsername, editEmail || null);
      setMessage({ text: `Profile updated for ${editUsername}.`, ok: true });
      setEditProfileUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update profile.", ok: false });
    }
  };

  // Role Change
  const openChangeRole = (u: UserRecord) => {
    setRoleDialogUser(u);
    setNewRoleId(u.roleId);
    setRoleReason("");
  };

  const handleSaveRole = async () => {
    if (!roleDialogUser || !newRoleId || !roleReason) {
      setMessage({ text: "Selected Role and a Reason are required for role change.", ok: false });
      return;
    }
    try {
      await UserService.changeRole(roleDialogUser.id, Number(newRoleId), roleReason);
      setMessage({ text: `Role changed for ${roleDialogUser.username}.`, ok: true });
      setRoleDialogUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not change role.", ok: false });
    }
  };

  // Enable/Disable
  const openStatusDialog = (u: UserRecord) => {
    setStatusDialogUser(u);
    setStatusReason("");
  };

  const handleSaveStatus = async () => {
    if (!statusDialogUser) return;
    const newStatus = !statusDialogUser.isActive;
    if (!newStatus && !statusReason) {
      setMessage({ text: "A reason is required to disable a user account.", ok: false });
      return;
    }
    try {
      await UserService.setStatus(statusDialogUser.id, newStatus, statusReason);
      setMessage({ text: `Account ${newStatus ? "enabled" : "disabled"} for ${statusDialogUser.username}.`, ok: true });
      setStatusDialogUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update account status.", ok: false });
    }
  };

  // Unlock
  const openUnlockDialog = (u: UserRecord) => {
    setUnlockDialogUser(u);
    setUnlockReason("");
  };

  const handleSaveUnlock = async () => {
    if (!unlockDialogUser) return;
    try {
      await UserService.unlock(unlockDialogUser.id, unlockReason);
      setMessage({ text: `Account unlocked for ${unlockDialogUser.username}.`, ok: true });
      setUnlockDialogUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not unlock account.", ok: false });
    }
  };

  // Standard Email Password Reset
  const openResetDialog = (u: UserRecord) => {
    setResetDialogUser(u);
    setResetReason("");
  };

  const handleSavePasswordReset = async () => {
    if (!resetDialogUser) return;
    try {
      await UserService.initiatePasswordReset(resetDialogUser.id, resetReason);
      setMessage({ text: `Password reset instructions initiated for ${resetDialogUser.username}.`, ok: true });
      setResetDialogUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not initiate password reset.", ok: false });
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
      setMessage({ text: "A reason is required for admin-assisted password recovery.", ok: false });
      return;
    }
    try {
      const result = await UserService.adminPasswordRecovery(adminRecoveryUser.id, adminRecoveryReason);
      setGeneratedCode(result.recoveryCode);
      setMessage({ text: `Recovery code generated for ${adminRecoveryUser.username}.`, ok: true });
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not generate recovery code.", ok: false });
    }
  };

  const handleCopyCode = () => {
    if (generatedCode) {
      navigator.clipboard.writeText(generatedCode);
      setCodeCopied(true);
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
  };

  const handleSaveForcePwd = async () => {
    if (!forcePwdUser) return;
    try {
      await UserService.forcePasswordChange(forcePwdUser.id);
      setMessage({ text: `Forced password change set for ${forcePwdUser.username}.`, ok: true });
      setForcePwdUser(null);
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not force password change.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="User Management" subtitle="Manage system users, role assignments, security status, and account access." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>{message.text}</Alert>}

      <SectionTitle>Create New User</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField size="small" label="Full Name" value={fullName} onChange={(e) => setFullName(e.target.value)} />
          <TextField size="small" label="Username" value={username} onChange={(e) => setUsername(e.target.value)} />
          <TextField size="small" label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} helperText="Password reset destination" />
          <TextField size="small" label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} helperText="Min 8 chars (upper, lower, digit, symbol)" />
          <Select size="small" displayEmpty value={roleId} onChange={(e) => setRoleId(e.target.value)} sx={{ minWidth: 200 }}>
            <MenuItem value=""><em>Select Role</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <Button variant="contained" color="primary" onClick={handleCreateUser}>Create User</Button>
        </Stack>
      </Paper>

      <SectionTitle>All System Users</SectionTitle>
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ backgroundColor: "action.hover" }}>
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
                    <Typography sx={{ color: "text.secondary", fontSize: 12 }}>@{u.username} {isSelf && <Chip label="You" size="small" color="primary" variant="outlined" sx={{ height: 18, fontSize: 10 }} />}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 13, fontStyle: u.email ? "normal" : "italic", color: u.email ? "text.primary" : "text.secondary" }}>
                      {u.email ?? "No email"}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip label={u.role?.name ?? "No role"} size="small" color={u.role?.type === "SystemAdministrator" ? "secondary" : "default"} />
                  </TableCell>
                  <TableCell>
                    {u.isActive ? (
                      <Chip label="Active" size="small" color="success" icon={<CheckCircleOutlineIcon fontSize="small" />} />
                    ) : (
                      <Chip label="Disabled" size="small" color="error" icon={<BlockIcon fontSize="small" />} />
                    )}
                  </TableCell>
                  <TableCell>
                    <Stack direction="column" spacing={0.5}>
                      {u.isLocked && <Chip label="Account Locked" size="small" color="warning" icon={<SecurityIcon fontSize="small" />} />}
                      {u.mustChangePassword && <Chip label="Must Change Password" size="small" color="info" />}
                      {!u.isLocked && !u.mustChangePassword && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Normal</Typography>}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                      <Tooltip title="Edit Profile">
                        <IconButton size="small" onClick={() => openEditProfile(u)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? "System Administrators cannot change their own role" : "Change Role"}>
                        <span>
                          <IconButton size="small" color="primary" disabled={isSelf} onClick={() => openChangeRole(u)}>
                            <PersonOutlineIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title={isSelf && u.isActive ? "System Administrators cannot disable their own account" : (u.isActive ? "Disable Account" : "Enable Account")}>
                        <span>
                          <IconButton size="small" color={u.isActive ? "error" : "success"} disabled={isSelf && u.isActive} onClick={() => openStatusDialog(u)}>
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
                          <IconButton size="small" color="warning" disabled={isSelf || !u.isActive} onClick={() => openAdminRecoveryDialog(u)}>
                            <KeyIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title="Force Password Change at Next Login">
                        <IconButton size="small" color="info" onClick={() => openForcePwdDialog(u)}><SecurityIcon fontSize="small" /></IconButton>
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
      <Dialog open={Boolean(editProfileUser)} onClose={() => setEditProfileUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Edit Profile — {editProfileUser?.username}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Full Name" size="small" value={editFullName} onChange={(e) => setEditFullName(e.target.value)} fullWidth />
            <TextField label="Username" size="small" value={editUsername} onChange={(e) => setEditUsername(e.target.value)} fullWidth />
            <TextField label="Email" size="small" type="email" value={editEmail} onChange={(e) => setEditEmail(e.target.value)} fullWidth helperText="Required for email password resets" />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditProfileUser(null)}>Cancel</Button>
          <Button variant="contained" onClick={handleSaveProfile}>Save Profile</Button>
        </DialogActions>
      </Dialog>

      {/* Change Role Dialog */}
      <Dialog open={Boolean(roleDialogUser)} onClose={() => setRoleDialogUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Change Role — {roleDialogUser?.username}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2">Current Role: <strong>{roleDialogUser?.role?.name ?? "None"}</strong></Typography>
            <Select size="small" value={newRoleId} onChange={(e) => setNewRoleId(Number(e.target.value))} fullWidth displayEmpty>
              <MenuItem value=""><em>Select New Role</em></MenuItem>
              {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
            </Select>
            <TextField label="Reason for Change" size="small" multiline rows={2} value={roleReason} onChange={(e) => setRoleReason(e.target.value)} required fullWidth placeholder="Mandatory administrative justification" />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRoleDialogUser(null)}>Cancel</Button>
          <Button variant="contained" color="primary" onClick={handleSaveRole} disabled={!newRoleId || !roleReason}>Change Role</Button>
        </DialogActions>
      </Dialog>

      {/* Enable / Disable Status Dialog */}
      <Dialog open={Boolean(statusDialogUser)} onClose={() => setStatusDialogUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{statusDialogUser?.isActive ? "Disable User Account" : "Enable User Account"}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2">
              Are you sure you want to {statusDialogUser?.isActive ? "disable" : "enable"} account <strong>{statusDialogUser?.fullName} (@{statusDialogUser?.username})</strong>?
            </Typography>
            {statusDialogUser?.isActive && (
              <TextField label="Reason for Disabling" size="small" multiline rows={2} value={statusReason} onChange={(e) => setStatusReason(e.target.value)} required fullWidth placeholder="Mandatory justification" />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusDialogUser(null)}>Cancel</Button>
          <Button variant="contained" color={statusDialogUser?.isActive ? "error" : "success"} onClick={handleSaveStatus} disabled={statusDialogUser?.isActive && !statusReason}>
            {statusDialogUser?.isActive ? "Disable Account" : "Enable Account"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Unlock Dialog */}
      <Dialog open={Boolean(unlockDialogUser)} onClose={() => setUnlockDialogUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Unlock User Account</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2">
              Unlock account for <strong>{unlockDialogUser?.fullName} (@{unlockDialogUser?.username})</strong>? This will clear failed login attempts and reset account lock.
            </Typography>
            <TextField label="Reason for Unlock (Optional)" size="small" value={unlockReason} onChange={(e) => setUnlockReason(e.target.value)} fullWidth />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setUnlockDialogUser(null)}>Cancel</Button>
          <Button variant="contained" color="warning" onClick={handleSaveUnlock}>Unlock Account</Button>
        </DialogActions>
      </Dialog>

      {/* Standard Email Reset Dialog */}
      <Dialog open={Boolean(resetDialogUser)} onClose={() => setResetDialogUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Initiate Email Password Reset</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2">
              Send password reset instructions to <strong>{resetDialogUser?.fullName} ({resetDialogUser?.email ?? "No email on file"})</strong>?
            </Typography>
            <Alert severity="info" sx={{ fontSize: 12 }}>
              The system will generate a secure reset token link and send it via email. Plaintext passwords are never generated or shown to administrators.
            </Alert>
            <TextField label="Reason for Reset (Optional)" size="small" value={resetReason} onChange={(e) => setResetReason(e.target.value)} fullWidth />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetDialogUser(null)}>Cancel</Button>
          <Button variant="contained" color="secondary" onClick={handleSavePasswordReset}>Send Reset Link</Button>
        </DialogActions>
      </Dialog>

      {/* Admin-Assisted Password Recovery Dialog */}
      <Dialog open={Boolean(adminRecoveryUser)} onClose={closeAdminRecoveryDialog} maxWidth="sm" fullWidth>
        <DialogTitle>Administrator-Assisted Password Recovery</DialogTitle>
        <DialogContent dividers>
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
              <Typography variant="subtitle2" color="text.secondary">
                Provide this recovery code to <strong>{adminRecoveryUser?.fullName}</strong>:
              </Typography>
              <Box
                sx={{
                  p: 2.5,
                  backgroundColor: "grey.100",
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
              <Stack direction="row" spacing={1} alignItems="center">
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
        </DialogContent>
        <DialogActions>
          {!generatedCode ? (
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
          )}
        </DialogActions>
      </Dialog>

      {/* Force Password Change Dialog */}
      <Dialog open={Boolean(forcePwdUser)} onClose={() => setForcePwdUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Force Password Change</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" sx={{ mt: 1 }}>
            Require <strong>{forcePwdUser?.fullName} (@{forcePwdUser?.username})</strong> to change their password on next login?
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setForcePwdUser(null)}>Cancel</Button>
          <Button variant="contained" color="info" onClick={handleSaveForcePwd}>Force Password Change</Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
