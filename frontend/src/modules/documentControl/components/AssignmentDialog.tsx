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

export function AssignmentDialog({
  open,
  onClose,
  onSuccess,
  documentMasterId,
  companyDocumentCode
}: AssignmentDialogProps) {
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
      fetchUsers();
    }
  }, [open]);

  const fetchUsers = async () => {
    setLoadingUsers(true);
    try {
      const res = await apiClient.get("/users/directory");
      const list: UserDirectoryOption[] = res.data?.data || res.data || [];
      setUsers(list.filter((u) => u.isActive !== false));
    } catch {
      try {
        const fallbackRes = await apiClient.get("/users");
        const fallbackList: any[] = fallbackRes.data?.data || fallbackRes.data || [];
        setUsers(
          fallbackList.map((u) => ({
            id: u.id,
            fullName: u.fullName,
            username: u.username,
            jobTitle: u.jobTitle,
            roleName: u.role?.name || u.roleName,
            isActive: u.isActive
          }))
        );
      } catch {
        setError("Failed to load active personnel directory.");
      }
    } finally {
      setLoadingUsers(false);
    }
  };

  const handleSave = async () => {
    if (!selectedUser || !selectedUser.id || selectedUser.id <= 0) {
      setError("Please select a target personnel.");
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
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to add assignment.";
      setError(msg);
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
          Add Role Assignment
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
        <Typography variant="caption" sx={{
          color: "text.secondary"
        }}>
          Document: <strong>{companyDocumentCode}</strong>
        </Typography>

        {error && <Alert severity="error">{error}</Alert>}

        <TextField
          select
          label="Assignment Role *"
          value={assignmentRole}
          onChange={(e) => setAssignmentRole(e.target.value as AssignmentRole)}
          disabled={loading}
          fullWidth
        >
          <MenuItem value="Author">Author (Can edit draft & upload files)</MenuItem>
          <MenuItem value="TechnicalReviewer">Technical Reviewer</MenuItem>
          <MenuItem value="Approver">Approver</MenuItem>
          <MenuItem value="Owner">Co-Owner</MenuItem>
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
              label="Select Personnel *"
              placeholder="Search by name, username, or title..."
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
                {option.jobTitle || "Personnel"} &bull; {option.roleName || "Staff"}
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
              Selected Personnel: <strong>{selectedUser.fullName}</strong> ({selectedUser.username})
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              Role/Title: {selectedUser.jobTitle || "N/A"} &bull; System Role: {selectedUser.roleName || "Staff"}
            </Typography>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={loading || !selectedUser}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? "Assigning..." : "Assign Role"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
