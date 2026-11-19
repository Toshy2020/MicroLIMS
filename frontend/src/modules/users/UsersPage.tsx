import { useEffect, useState } from "react";
import {
  Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, Box,
  Chip, Tooltip, Table, Tabs, Tab,
  TableBody, TableCell, TableContainer, TableHead, TableRow, IconButton
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { tableHeadSx } from "../../theme";
import { FloatingDialog } from "../../components/FloatingDialog";
import { Ltr } from "../../components/Ltr";
import { SignatureDialog } from "../../components/SignatureDialog";
import EditIcon from "@mui/icons-material/Edit";
import LockOpenIcon from "@mui/icons-material/LockOpen";
import LockResetIcon from "@mui/icons-material/LockReset";
import SecurityIcon from "@mui/icons-material/Security";
import BlockIcon from "@mui/icons-material/Block";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";
import KeyIcon from "@mui/icons-material/Key";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import AccountTreeIcon from "@mui/icons-material/AccountTree";

import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { UserService, UserRecord, SignInMethod } from "./services/UserService";
import { SignInFields, getLinkStateLabels } from "./SignInFields";
import { RelinkDirectoryDialog } from "./dialogs/RelinkDirectoryDialog";
import LinkIcon from "@mui/icons-material/Link";
import { RoleService, RoleRecord } from "../roles/services/RoleService";
import { useAuth } from "../../contexts/AuthContext";
import { i18n, useTranslation } from "../../i18n";
import { serverMessage } from "../../utils/errorMessage";
import { useLoadFailures } from "../../hooks/useLoadFailures";
import { UserSectionsDialog } from "./dialogs/UserSectionsDialog";
import { UserSessionsDialog } from "./dialogs/UserSessionsDialog";
import { ResetMfaDialog } from "./dialogs/ResetMfaDialog";
import { AssignHardwareTokenDialog } from "./dialogs/AssignHardwareTokenDialog";
import DevicesIcon from "@mui/icons-material/Devices";
import PhonelinkEraseIcon from "@mui/icons-material/PhonelinkErase";
import HardwareIcon from "@mui/icons-material/Memory";
import { LoadFailuresAlert } from "../../components/LoadErrorAlert";
import { MetaBadge, ToneBadge } from "../../components/StatusBadge";
import { getMfaStateDisplay } from "../../utils/mfaStateDisplay";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

export function UsersPage() {
  const theme = useTheme();
  const { t } = useTranslation("users");
  const { t: tc } = useTranslation("common");
  const tEn = i18n.getFixedT("en", "users");
  const linkStateLabels = getLinkStateLabels(t);
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
  const [signInMethod, setSignInMethod] = useState<SignInMethod>("Local");
  const [directoryUsername, setDirectoryUsername] = useState("");
  const [isEmergency, setIsEmergency] = useState(false);

  // Dialog state variables
  const [editProfileUser, setEditProfileUser] = useState<UserRecord | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editUsername, setEditUsername] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editJobTitle, setEditJobTitle] = useState("");
  const [editReason, setEditReason] = useState("");
  const [editMethod, setEditMethod] = useState<SignInMethod>("Local");
  const [editDirectoryUsername, setEditDirectoryUsername] = useState("");
  const [editEmergency, setEditEmergency] = useState(false);
  const [relinkUser, setRelinkUser] = useState<UserRecord | null>(null);

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

  // Role change is electronically signed (password re-entry).
  const [signing, setSigning] = useState<"role" | null>(null);
  // Inactive users are listed apart and only here, for reactivation.
  const [view, setView] = useState<"active" | "inactive">("active");

  const [sectionsUser, setSectionsUser] = useState<UserRecord | null>(null);
  const [sessionsUser, setSessionsUser] = useState<UserRecord | null>(null);
  const [resetMfaUser, setResetMfaUser] = useState<UserRecord | null>(null);
  const [assignTokenUser, setAssignTokenUser] = useState<UserRecord | null>(null);

  // A failed load used to leave the user table (or the role picker) empty
  // with no explanation.
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();
  const load = (which: "active" | "inactive" = view) => {
    resetFailures();
    UserService.getAll(which).then(setUsers).catch(fail("users"));
    RoleService.getAll().then(setRoles).catch(fail("roles"));
  };

  // Reloads on mount and when the Active/Inactive tab changes; load is redefined each render and only runs set-state calls.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { load(view); }, [view]);

  const isAdministratorRole = (id: number | string) => roles.find((r) => String(r.id) === String(id))?.type === "SystemAdministrator";
  const createIsDirectory = signInMethod === "Directory";

  const handleCreateUser = async () => {
    if (!fullName || !username || (!createIsDirectory && !password) || !roleId || !createReason.trim()) {
      toast.error(createIsDirectory ? t("create.missingRequiredDirectory") : t("create.missingRequiredLocal"));
      return;
    }
    if (createIsDirectory && !directoryUsername.trim()) {
      toast.error(t("create.missingDirectoryUsername"));
      return;
    }
    try {
      await UserService.create(fullName, username, createIsDirectory ? "" : password, Number(roleId), createReason.trim(), email, jobTitle, {
        signInMethod,
        directoryUsername: createIsDirectory ? directoryUsername.trim() : null,
        isEmergencyAccount: isEmergency && isAdministratorRole(roleId)
      });
      toast.success(t("create.success", { username }));
      setFullName(""); setUsername(""); setPassword(""); setEmail(""); setJobTitle(""); setRoleId(""); setCreateReason("");
      setSignInMethod("Local"); setDirectoryUsername(""); setIsEmergency(false);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("create.error"));
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
    setEditMethod(u.signInMethod ?? "Local");
    setEditDirectoryUsername(u.directoryUsername ?? "");
    setEditEmergency(Boolean(u.isEmergencyAccount));
  };

  const handleSaveProfile = async () => {
    if (!editProfileUser) return;
    if (!editReason.trim()) {
      toast.error(t("editProfile.reasonRequired"));
      return;
    }
    if (editProfileUser.signInMethod && editMethod === "Directory" && !editDirectoryUsername.trim()) {
      toast.error(t("editProfile.directoryRequired"));
      return;
    }
    try {
      if (editProfileUser.signInMethod) {
        await UserService.updateProfile(editProfileUser.id, editFullName, editUsername, editEmail || null, editJobTitle || null, editReason.trim(), editProfileUser.version, {
          signInMethod: editMethod,
          directoryUsername: editMethod === "Directory" ? editDirectoryUsername.trim() : null,
          isEmergencyAccount: editEmergency && isAdministratorRole(editProfileUser.roleId)
        });
      } else {
        await UserService.updateProfile(editProfileUser.id, editFullName, editUsername, editEmail || null, editJobTitle || null, editReason.trim(), editProfileUser.version);
      }
      toast.success(t("editProfile.success", { username: editUsername }));
      setEditProfileUser(null);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("editProfile.error"));
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
      toast.error(t("roleChange.required"));
      return;
    }
    setSigning("role");
  };

  // A failure is thrown back to the signature dialog, which shows it and stays open.
  const confirmRoleSignature = async (signaturePassword: string) => {
    if (!roleDialogUser || !newRoleId) return;
    await UserService.changeRole(roleDialogUser.id, Number(newRoleId), roleReason.trim(), signaturePassword);
    toast.success(t("roleChange.success", { username: roleDialogUser.username }));
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
      toast.error(newStatus ? t("statusDialog.enableReasonRequired") : t("statusDialog.disableReasonRequired"));
      return;
    }
    try {
      await UserService.setStatus(statusDialogUser.id, newStatus, statusReason.trim());
      toast.success(newStatus
        ? t("statusDialog.enableSuccess", { username: statusDialogUser.username })
        : t("statusDialog.disableSuccess", { username: statusDialogUser.username }));
      setStatusDialogUser(null);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("statusDialog.error"));
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
      toast.error(t("unlock.reasonRequired"));
      return;
    }
    try {
      await UserService.unlock(unlockDialogUser.id, unlockReason.trim());
      toast.success(t("unlock.success", { username: unlockDialogUser.username }));
      setUnlockDialogUser(null);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("unlock.error"));
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
      toast.error(t("emailReset.reasonRequired"));
      return;
    }
    try {
      await UserService.initiatePasswordReset(resetDialogUser.id, resetReason.trim());
      toast.success(t("emailReset.success", { username: resetDialogUser.username }));
      setResetDialogUser(null);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("emailReset.error"));
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
      toast.error(t("adminRecovery.reasonRequired"));
      return;
    }
    try {
      const result = await UserService.adminPasswordRecovery(adminRecoveryUser.id, adminRecoveryReason);
      setGeneratedCode(result.recoveryCode);
      toast.success(t("adminRecovery.generatedSuccess", { username: adminRecoveryUser.username }));
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("adminRecovery.generateError"));
    }
  };

  const handleCopyCode = () => {
    if (generatedCode) {
      navigator.clipboard.writeText(generatedCode);
      setCodeCopied(true);
      toast.success(t("adminRecovery.copiedToast"));
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
      toast.error(t("forcePassword.reasonRequired"));
      return;
    }
    try {
      await UserService.forcePasswordChange(forcePwdUser.id, forcePwdReason.trim());
      toast.success(t("forcePassword.success", { username: forcePwdUser.username }));
      setForcePwdUser(null);
      load();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? t("forcePassword.error"));
    }
  };

  return (
    <>
      <PageHeader title={t("page.title")} subtitle={t("page.subtitle")} />
      <LoadFailuresAlert failed={failedLists} retryHint={t("page.retryHint")} sx={{ mb: 2 }} />
      <SectionTitle>{t("create.sectionTitle")}</SectionTitle>
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
            label={t("fields.fullName")}
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            required
            fullWidth
          />
          <TextField
            size="small"
            label={t("fields.username")}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
            fullWidth
          />
          <TextField
            size="small"
            label={t("fields.email")}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            helperText={t("create.emailHelper")}
            fullWidth
          />
          <TextField
            size="small"
            label={t("fields.jobTitle")}
            value={jobTitle}
            onChange={(e) => setJobTitle(e.target.value)}
            fullWidth
          />
          {!createIsDirectory && (
            <TextField
              size="small"
              label={t("fields.password")}
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              helperText={t("create.passwordHelper")}
              required
              fullWidth
            />
          )}
          <Select
            size="small"
            displayEmpty
            value={roleId}
            onChange={(e) => setRoleId(e.target.value)}
            fullWidth
            inputProps={{ "aria-label": t("fields.role") }}
          >
            <MenuItem value=""><em>{t("create.selectRole")}</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <TextField
            size="small"
            label={t("fields.reason")}
            value={createReason}
            onChange={(e) => setCreateReason(e.target.value)}
            helperText={t("create.reasonHelper")}
            required
            fullWidth
          />
          <SignInFields
            idPrefix="create"
            method={signInMethod}
            onMethodChange={setSignInMethod}
            directoryUsername={directoryUsername}
            onDirectoryUsernameChange={setDirectoryUsername}
            emergency={isEmergency}
            onEmergencyChange={setIsEmergency}
            roleIsAdministrator={isAdministratorRole(roleId)}
          />
          <Button
            type="submit"
            variant="contained"
            color="primary"
            sx={{ height: 40, whiteSpace: "nowrap" }}
          >
            {t("create.submit")}
          </Button>
        </Box>
      </Paper>

      <SectionTitle>{t("list.sectionTitle")}</SectionTitle>
      <Tabs value={view} onChange={(_, v) => { setUsers([]); setView(v); }} sx={{ mb: 1 }} aria-label={t("list.tabsLabel")}>
        <Tab value="active" label={t("list.tabActive")} />
        <Tab value="inactive" label={t("list.tabInactive")} />
      </Tabs>
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell>{t("list.columns.user")}</TableCell>
              <TableCell>{t("list.columns.email")}</TableCell>
              <TableCell>{t("list.columns.role")}</TableCell>
              <TableCell>{t("list.columns.signIn")}</TableCell>
              <TableCell>{t("list.columns.status")}</TableCell>
              <TableCell>{t("list.columns.securityPassword")}</TableCell>
              <TableCell>{t("list.columns.secondFactor")}</TableCell>
              <TableCell>{t("list.columns.lastLogin")}</TableCell>
              <TableCell align="right">{t("list.columns.actions")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => {
              const isSelf = u.id === currentUserId;
              const mfaDisplay = getMfaStateDisplay(u.mfaState, tc);
              return (
                <TableRow key={u.id} hover>
                  <TableCell>
                    <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{u.fullName}</Typography>
                    <Typography sx={{ color: "text.secondary", fontSize: 12 }}>@{u.username} {isSelf && <Chip label={t("list.youBadge")} size="small" color="primary" variant="outlined" sx={{ height: 20, fontSize: 12 }} />}</Typography>
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 13, fontStyle: u.email ? "normal" : "italic", color: u.email ? "text.primary" : "text.secondary" }}>
                      {u.email ?? t("list.noEmail")}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <MetaBadge label={u.role?.name ?? t("list.noRole")} />
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 13 }}>{u.signInMethod === "Directory" ? t("fields.directoryLogin") : t("fields.localLogin")}</Typography>
                    {u.signInMethod === "Directory" && (
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{u.linkState ? (linkStateLabels[u.linkState] ?? u.linkState) : t("linkState.unknown")}</Typography>
                    )}
                    {u.isEmergencyAccount && <Box sx={{ mt: 0.5 }}><ToneBadge tone="action" label={t("fields.emergencyAccount")} /></Box>}
                  </TableCell>
                  <TableCell>
                    {u.isActive ? (
                      <ToneBadge tone="notDetected" label={t("list.statusActive")} />
                    ) : (
                      <ToneBadge tone="detected" label={t("list.statusDisabled")} />
                    )}
                  </TableCell>
                  <TableCell>
                    <Stack direction="column" spacing={0.5}>
                      {u.isLocked && <Box><ToneBadge tone="action" label={t("list.accountLocked")} /></Box>}
                      {u.mustChangePassword && <Box><ToneBadge tone="info" label={t("list.mustChangePassword")} /></Box>}
                      {!u.isLocked && !u.mustChangePassword && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("list.securityNormal")}</Typography>}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    {mfaDisplay ? (
                      <ToneBadge tone={mfaDisplay.tone} label={mfaDisplay.label} />
                    ) : (
                      <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {u.lastLoginAt ? formatDateTimeWithTimeZone(u.lastLoginAt) : t("list.neverLoggedIn")}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    {view === "inactive" ? (
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                        <Tooltip title={t("rowActions.reactivateAccount")}>
                          <IconButton aria-label={t("rowActions.reactivateAccount")} size="small" color="success" onClick={() => openStatusDialog(u)}>
                            <BlockIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </Stack>
                    ) : (
                    <Stack direction="row" spacing={0.5} sx={{
                      justifyContent: "flex-end"
                    }}>
                      <Tooltip title={t("rowActions.editProfile")}>
                        <IconButton aria-label={t("rowActions.editProfile")} size="small" onClick={() => openEditProfile(u)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title={t("rowActions.laboratorySections")}>
                        <IconButton aria-label={t("rowActions.laboratorySections")} size="small" color="info" onClick={() => setSectionsUser(u)}>
                          <AccountTreeIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      <Tooltip title={t("rowActions.sessions")}>
                        <IconButton aria-label={t("rowActions.sessions")} size="small" onClick={() => setSessionsUser(u)}>
                          <DevicesIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? t("rowActions.cannotChangeOwnRole") : t("rowActions.changeRole")}>
                        <span>
                          <IconButton aria-label={isSelf ? t("rowActions.cannotChangeOwnRole") : t("rowActions.changeRole")} size="small" color="primary" disabled={isSelf} onClick={() => openChangeRole(u)}>
                            <PersonOutlineIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title={isSelf && u.isActive ? t("rowActions.cannotDisableOwnAccount") : (u.isActive ? t("rowActions.disableAccount") : t("rowActions.enableAccount"))}>
                        <span>
                          <IconButton aria-label={isSelf && u.isActive ? t("rowActions.cannotDisableOwnAccount") : (u.isActive ? t("rowActions.disableAccount") : t("rowActions.enableAccount"))} size="small" color={u.isActive ? "error" : "success"} disabled={isSelf && u.isActive} onClick={() => openStatusDialog(u)}>
                            <BlockIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      {u.isLocked && (
                        <Tooltip title={t("rowActions.unlockAccount")}>
                          <IconButton aria-label={t("rowActions.unlockAccount")} size="small" color="warning" onClick={() => openUnlockDialog(u)}><LockOpenIcon fontSize="small" /></IconButton>
                        </Tooltip>
                      )}

                      <Tooltip title={t("rowActions.resetPasswordEmail")}>
                        <IconButton aria-label={t("rowActions.resetPasswordEmail")} size="small" color="secondary" onClick={() => openResetDialog(u)}><LockResetIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      <Tooltip title={isSelf ? t("rowActions.cannotRecoverOwnAccount") : (!u.isActive ? t("rowActions.enableFirstRecovery") : t("rowActions.adminRecovery"))}>
                        <span>
                          <IconButton aria-label={isSelf ? t("rowActions.cannotRecoverOwnAccount") : (!u.isActive ? t("rowActions.enableFirstRecovery") : t("rowActions.adminRecovery"))} size="small" color="warning" disabled={isSelf || !u.isActive} onClick={() => openAdminRecoveryDialog(u)}>
                            <KeyIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>

                      <Tooltip title={t("rowActions.forcePasswordChange")}>
                        <IconButton aria-label={t("rowActions.forcePasswordChange")} size="small" color="info" onClick={() => openForcePwdDialog(u)}><SecurityIcon fontSize="small" /></IconButton>
                      </Tooltip>

                      {u.signInMethod === "Directory" && (
                        <Tooltip title={t("rowActions.relinkCompany")}>
                          <IconButton aria-label={t("rowActions.relinkCompanyFor", { username: u.username })} size="small" color="warning" onClick={() => setRelinkUser(u)}>
                            <LinkIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}

                      {!isSelf && (
                        <>
                          <Tooltip title={t("rowActions.resetSecondFactor")}>
                            <IconButton
                              aria-label={t("rowActions.resetSecondFactorFor", { username: u.username })}
                              size="small"
                              color="error"
                              onClick={() => setResetMfaUser(u)}
                            >
                              <PhonelinkEraseIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title={t("rowActions.assignHardwareToken")}>
                            <IconButton
                              aria-label={t("rowActions.assignHardwareTokenFor", { username: u.username })}
                              size="small"
                              color="primary"
                              onClick={() => setAssignTokenUser(u)}
                            >
                              <HardwareIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </>
                      )}
                    </Stack>
                    )}
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
        title={t("editProfile.title", { username: editProfileUser?.username ?? "" })}
        actions={
          <>
            <Button onClick={() => setEditProfileUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={handleSaveProfile} disabled={!editReason.trim()}>{t("editProfile.save")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label={t("fields.fullName")} size="small" value={editFullName} onChange={(e) => setEditFullName(e.target.value)} fullWidth />
          <TextField label={t("fields.username")} size="small" value={editUsername} onChange={(e) => setEditUsername(e.target.value)} fullWidth />
          <TextField label={t("fields.email")} size="small" type="email" value={editEmail} onChange={(e) => setEditEmail(e.target.value)} fullWidth helperText={t("editProfile.emailHelper")} />
          <TextField label={t("fields.jobTitle")} size="small" value={editJobTitle} onChange={(e) => setEditJobTitle(e.target.value)} fullWidth />
          {editProfileUser?.signInMethod && (
            <SignInFields
              idPrefix="edit"
              method={editMethod}
              onMethodChange={setEditMethod}
              directoryUsername={editDirectoryUsername}
              onDirectoryUsernameChange={setEditDirectoryUsername}
              emergency={editEmergency}
              onEmergencyChange={setEditEmergency}
              roleIsAdministrator={isAdministratorRole(editProfileUser.roleId)}
            />
          )}
          <TextField label={t("fields.reasonForChange")} size="small" multiline rows={2} value={editReason} onChange={(e) => setEditReason(e.target.value)} required fullWidth placeholder={t("fields.reasonPlaceholder")} />
        </Stack>
      </FloatingDialog>

      {/* Change Role Dialog */}
      <FloatingDialog
        open={Boolean(roleDialogUser)}
        onClose={() => setRoleDialogUser(null)}
        maxWidth="xs"
        title={t("roleChange.title", { username: roleDialogUser?.username ?? "" })}
        actions={
          <>
            <Button onClick={() => setRoleDialogUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" color="primary" onClick={handleSaveRole} disabled={!newRoleId || !roleReason.trim()}>{t("roleChange.submit")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            <Trans i18nKey="roleChange.currentRole" ns="users" values={{ role: roleDialogUser?.role?.name ?? t("roleChange.none") }} components={{ role: <strong /> }} />
          </Typography>
          <Select size="small" value={newRoleId} onChange={(e) => setNewRoleId(Number(e.target.value))} fullWidth displayEmpty inputProps={{ "aria-label": t("roleChange.newRoleLabel") }}>
            <MenuItem value=""><em>{t("roleChange.selectNewRole")}</em></MenuItem>
            {roles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </Select>
          <TextField label={t("fields.reasonForChange")} size="small" multiline rows={2} value={roleReason} onChange={(e) => setRoleReason(e.target.value)} required fullWidth placeholder={t("fields.reasonPlaceholder")} />
        </Stack>
      </FloatingDialog>

      {/* Enable / Disable Status Dialog */}
      <FloatingDialog
        open={Boolean(statusDialogUser)}
        onClose={() => setStatusDialogUser(null)}
        maxWidth="xs"
        title={statusDialogUser?.isActive ? t("statusDialog.disableTitle") : t("statusDialog.reactivateTitle")}
        actions={
          <>
            <Button onClick={() => setStatusDialogUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" color={statusDialogUser?.isActive ? "error" : "success"} onClick={handleSaveStatus} disabled={!statusReason.trim()}>
              {statusDialogUser?.isActive ? t("statusDialog.disableButton") : t("statusDialog.reactivateButton")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            <Trans
              i18nKey={statusDialogUser?.isActive ? "statusDialog.disableConfirm" : "statusDialog.enableConfirm"}
              ns="users"
              values={{ fullName: statusDialogUser?.fullName ?? "", username: statusDialogUser?.username ?? "" }}
              components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <TextField label={statusDialogUser?.isActive ? t("statusDialog.disableReasonLabel") : t("statusDialog.enableReasonLabel")} size="small" multiline rows={2} value={statusReason} onChange={(e) => setStatusReason(e.target.value)} required fullWidth placeholder={t("fields.reasonJustificationPlaceholder")} />
        </Stack>
      </FloatingDialog>

      {/* Unlock Dialog */}
      <FloatingDialog
        open={Boolean(unlockDialogUser)}
        onClose={() => setUnlockDialogUser(null)}
        maxWidth="xs"
        title={t("unlock.title")}
        actions={
          <>
            <Button onClick={() => setUnlockDialogUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" color="warning" onClick={handleSaveUnlock} disabled={!unlockReason.trim()}>{t("unlock.submit")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            <Trans
              i18nKey="unlock.confirm"
              ns="users"
              values={{ fullName: unlockDialogUser?.fullName ?? "", username: unlockDialogUser?.username ?? "" }}
              components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <TextField label={t("unlock.reasonLabel")} size="small" value={unlockReason} onChange={(e) => setUnlockReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Standard Email Reset Dialog */}
      <FloatingDialog
        open={Boolean(resetDialogUser)}
        onClose={() => setResetDialogUser(null)}
        maxWidth="xs"
        title={t("emailReset.title")}
        actions={
          <>
            <Button onClick={() => setResetDialogUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" color="secondary" onClick={handleSavePasswordReset} disabled={!resetReason.trim()}>{t("emailReset.submit")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            <Trans
              i18nKey="emailReset.confirm"
              ns="users"
              values={{ fullName: resetDialogUser?.fullName ?? "", email: resetDialogUser?.email ?? t("emailReset.noEmailOnFile") }}
              components={{ user: <strong /> }}
            />
          </Typography>
          <Alert severity="info" sx={{ fontSize: 12 }}>
            {t("emailReset.info")}
          </Alert>
          <TextField label={t("emailReset.reasonLabel")} size="small" value={resetReason} onChange={(e) => setResetReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Admin-Assisted Password Recovery Dialog */}
      <FloatingDialog
        open={Boolean(adminRecoveryUser)}
        onClose={closeAdminRecoveryDialog}
        maxWidth="sm"
        title={t("adminRecovery.title")}
        actions={
          !generatedCode ? (
            <>
              <Button onClick={closeAdminRecoveryDialog}>{tc("actions.cancel")}</Button>
              <Button
                variant="contained"
                color="warning"
                onClick={handleGenerateRecoveryCode}
                disabled={!adminRecoveryReason}
              >
                {t("adminRecovery.generate")}
              </Button>
            </>
          ) : (
            <Button variant="contained" onClick={closeAdminRecoveryDialog}>
              {t("adminRecovery.closeDone")}
            </Button>
          )
        }
      >
          {!generatedCode ? (
            <Stack spacing= {2} sx={{ mt: 1 }}>
              <Typography variant="body2">
                <Trans
                  i18nKey="adminRecovery.intro"
                  ns="users"
                  values={{ fullName: adminRecoveryUser?.fullName ?? "", username: adminRecoveryUser?.username ?? "" }}
                  components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              <Alert severity="warning" sx={{ fontSize: 12 }}>
                <Trans i18nKey="adminRecovery.warning" ns="users" components={{ bold: <strong /> }} />
              </Alert>
              <TextField
                label={t("adminRecovery.reasonLabel")}
                size="small"
                multiline
                rows={2}
                value={adminRecoveryReason}
                onChange={(e) => setAdminRecoveryReason(e.target.value)}
                required
                fullWidth
                placeholder={t("adminRecovery.reasonPlaceholder")}
              />
            </Stack>
          ) : (
            <Stack spacing={2.5} sx={{ mt: 1, alignItems: "center", textAlign: "center" }}>
              <Alert severity="success" sx={{ width: "100%" }}>
                {t("adminRecovery.generatedBanner")}
              </Alert>
              <Typography variant="subtitle2" sx={{
                color: "text.secondary"
              }}>
                <Trans
                  i18nKey="adminRecovery.provideCodeTo"
                  ns="users"
                  values={{ fullName: adminRecoveryUser?.fullName ?? "" }}
                  components={{ user: <strong /> }}
                />
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
                  {codeCopied ? t("adminRecovery.copied") : t("adminRecovery.copyCode")}
                </Button>
                <Chip label={t("adminRecovery.expiresChip")} color="warning" size="small" />
              </Stack>
              <Alert severity="error" sx={{ fontSize: 12, textAlign: "left", width: "100%" }}>
                <Trans i18nKey="adminRecovery.importantNote" ns="users" components={{ bold: <strong /> }} />
              </Alert>
            </Stack>
          )}
      </FloatingDialog>

      {/* Force Password Change Dialog */}
      <FloatingDialog
        open={Boolean(forcePwdUser)}
        onClose={() => setForcePwdUser(null)}
        maxWidth="xs"
        title={t("forcePassword.title")}
        actions={
          <>
            <Button onClick={() => setForcePwdUser(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" color="info" onClick={handleSaveForcePwd} disabled={!forcePwdReason.trim()}>{t("forcePassword.submit")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2">
            <Trans
              i18nKey="forcePassword.confirm"
              ns="users"
              values={{ fullName: forcePwdUser?.fullName ?? "", username: forcePwdUser?.username ?? "" }}
              components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <TextField label={t("fields.reason")} size="small" value={forcePwdReason} onChange={(e) => setForcePwdReason(e.target.value)} required fullWidth />
        </Stack>
      </FloatingDialog>

      {/* Electronic signature for the role change */}
      <SignatureDialog
        open={signing === "role"}
        meaningStatement={tEn("roleChange.signatureMeaning", {
          username: roleDialogUser?.username ?? tEn("roleChange.thisUser"),
          roleName: roles.find((r) => r.id === newRoleId)?.name ?? tEn("roleChange.theSelectedRole"),
          reason: roleReason.trim()
        })}
        meaningStatementTranslation={t("roleChange.signatureMeaning", {
          username: roleDialogUser?.username ?? t("roleChange.thisUser"),
          roleName: roles.find((r) => r.id === newRoleId)?.name ?? t("roleChange.theSelectedRole"),
          reason: roleReason.trim()
        })}
        onCancel={() => setSigning(null)}
        onConfirm={confirmRoleSignature}
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

      <RelinkDirectoryDialog
        open={Boolean(relinkUser)}
        onClose={() => setRelinkUser(null)}
        user={relinkUser}
        onRelinked={() => load()}
      />

      <ResetMfaDialog
        open={Boolean(resetMfaUser)}
        onClose={() => setResetMfaUser(null)}
        user={resetMfaUser}
        onReset={() => load()}
      />

      <AssignHardwareTokenDialog
        open={Boolean(assignTokenUser)}
        onClose={() => setAssignTokenUser(null)}
        user={assignTokenUser}
        onAssigned={() => load()}
      />
    </>
  );
}
