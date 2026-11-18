import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  TextField,
  MenuItem,
  Alert,
  CircularProgress,
  Autocomplete
} from "@mui/material";
import PersonAddOutlinedIcon from "@mui/icons-material/PersonAddOutlined";
import { documentControlService } from "../services/documentControlService";
import { apiClient } from "../../../services/apiClient";
import type { AssignmentRole, CreateAssignmentRequest } from "../types/documentControlTypes";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

interface AssignmentDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  documentMasterId: number;
  companyDocumentCode: string;
}

interface UserDirectoryOption {
  id: number;
  fullName: string;
  username: string;
  jobTitle?: string | null;
  roleName?: string;
  isActive?: boolean;
}

interface FallbackUserOption extends UserDirectoryOption {
  role?: { name?: string };
}

export function AssignmentDialog({
  open,
  onClose,
  onSuccess,
  documentMasterId,
  companyDocumentCode
}: AssignmentDialogProps) {
  const { t } = useTranslation("documentControl");
  const [selectedUser, setSelectedUser] = useState<UserDirectoryOption | null>(null);
  const [assignmentRole, setAssignmentRole] = useState<AssignmentRole>("Author");
  const [users, setUsers] = useState<UserDirectoryOption[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setSelectedUser(null);
      setError(null);
    }
  }, [open]);

  // Only users whose role holds the code the assigned function needs are offered (the server enforces the same rule).
  const requiredPermission =
    assignmentRole === "Author" ? "Documents.DraftEdit"
    : assignmentRole === "TechnicalReviewer" ? "Documents.Review"
    : assignmentRole === "Approver" ? "Documents.Approve"
    : undefined;

  useEffect(() => {
    if (open) {
      setSelectedUser(null);
      fetchUsers();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, requiredPermission]);

  const fetchUsers = async () => {
    setLoadingUsers(true);
    try {
      const res = await apiClient.get("/users/directory", requiredPermission ? { params: { permission: requiredPermission } } : undefined);
      const list: UserDirectoryOption[] = res.data?.data || res.data || [];
      setUsers(list.filter((u) => u.isActive !== false));
    } catch {
      try {
        const fallbackRes = await apiClient.get("/users");
        const fallbackList: FallbackUserOption[] = fallbackRes.data?.data || fallbackRes.data || [];
        setUsers(
          fallbackList.filter((u) => u.isActive !== false).map((u) => ({
            id: u.id,
            fullName: u.fullName,
            username: u.username,
            jobTitle: u.jobTitle,
            roleName: u.role?.name || u.roleName,
            isActive: u.isActive
          }))
        );
      } catch {
        setError(t("assignmentDialog.loadError"));
      }
    } finally {
      setLoadingUsers(false);
    }
  };

  const handleSave = async () => {
    if (!selectedUser || !selectedUser.id || selectedUser.id <= 0) {
      setError(t("assignmentDialog.selectPersonnelError"));
      return;
    }

    setLoading(true);
    setError(null);

    const req: CreateAssignmentRequest = {
      userId: selectedUser.id,
      assignmentRole
    };

    try {
      await documentControlService.addAssignment(documentMasterId, req);
      setSelectedUser(null);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("assignmentDialog.addError")));
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setSelectedUser(null);
      setError(null);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <PersonAddOutlinedIcon color="primary" />
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {t("assignmentDialog.title")}
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
        <Typography variant="caption" sx={{
          color: "text.secondary"
        }}>
          <Trans
            i18nKey="assignmentDialog.documentLine"
            ns="documentControl"
            values={{ code: companyDocumentCode }}
            components={{ strong: <strong /> }}
          />
        </Typography>

        {error && <Alert severity="error">{error}</Alert>}

        <TextField
          select
          label={t("assignmentDialog.roleLabel")}
          value={assignmentRole}
          onChange={(e) => setAssignmentRole(e.target.value as AssignmentRole)}
          disabled={loading}
          fullWidth
        >
          <MenuItem value="Author">{t("assignmentDialog.roles.Author")}</MenuItem>
          <MenuItem value="TechnicalReviewer">{t("assignmentDialog.roles.TechnicalReviewer")}</MenuItem>
          <MenuItem value="Approver">{t("assignmentDialog.roles.Approver")}</MenuItem>
          <MenuItem value="Owner">{t("assignmentDialog.roles.Owner")}</MenuItem>
        </TextField>

        <Autocomplete
          options={users}
          loading={loadingUsers}
          getOptionLabel={(option) =>
            `${option.fullName} (${option.username})${option.jobTitle ? ` - ${option.jobTitle}` : ""}`
          }
          isOptionEqualToValue={(option, val) => option.id === val.id}
          value={selectedUser}
          onChange={(_, newVal) => setSelectedUser(newVal)}
          disabled={loading || loadingUsers}
          renderInput={(params) => (
            <TextField
              {...params}
              label={t("assignmentDialog.selectPersonnelLabel")}
              placeholder={t("assignmentDialog.searchPlaceholder")}
              fullWidth
              slotProps={{
                ...params.slotProps,

                input: {
                  ...params.slotProps.input,
                  endAdornment: (
                    <>
                      {loadingUsers ? <CircularProgress color="inherit" size={20} /> : null}
                      {params.slotProps.input.endAdornment}
                    </>
                  )
                }
              }}
            />
          )}
          renderOption={(props, option) => (
            <Box component="li" {...props} key={option.id} sx={{ display: "flex", flexDirection: "column", py: 1 }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {option.fullName}{" "}
                <Typography component="span" variant="caption" sx={{
                  color: "text.secondary"
                }}>
                  ({option.username})
                </Typography>
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {option.jobTitle || t("assignmentDialog.personnelFallback")} &bull; {option.roleName || t("assignmentDialog.staffFallback")}
              </Typography>
            </Box>
          )}
        />

        {selectedUser && (
          <Box sx={{ p: 1.5, bgcolor: "action.hover", borderRadius: 1 }}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block"
              }}>
              <Trans
                i18nKey="assignmentDialog.selectedLine"
                ns="documentControl"
                values={{ name: selectedUser.fullName, username: selectedUser.username }}
                components={{ strong: <strong /> }}
              />
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {t("assignmentDialog.roleTitleLine", { jobTitle: selectedUser.jobTitle || t("common.na"), roleName: selectedUser.roleName || t("assignmentDialog.staffFallback") })}
            </Typography>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={loading || !selectedUser}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? t("assignmentDialog.assigning") : t("assignmentDialog.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
