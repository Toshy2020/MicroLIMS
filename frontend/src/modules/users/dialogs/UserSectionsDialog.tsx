import { useEffect, useState } from "react";
import {
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Paper,
  Stack,
  Typography,
  Alert,
  Divider
} from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { UserRecord } from "../services/UserService";
import {
  getSections,
  getUserMemberships,
  replaceUserMemberships,
  LaboratorySection,
  UserMembership
} from "../../../services/laboratorySectionService";
import { brandColors } from "../../../theme";

interface UserSectionsDialogProps {
  open: boolean;
  onClose: () => void;
  user: UserRecord | null;
  onSuccess: (message: string) => void;
}

interface DepartmentGroup {
  departmentId: number;
  departmentName: string;
  departmentCode: string;
  sections: {
    sectionId: number;
    sectionName: string;
    sectionCode: string;
  }[];
}

export function UserSectionsDialog({ open, onClose, user, onSuccess }: UserSectionsDialogProps) {
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [memberships, setMemberships] = useState<UserMembership[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !user) {
      setMemberships([]);
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);

    Promise.all([getSections(), getUserMemberships(user.id)])
      .then(([allSections, userMemberships]) => {
        setSections(allSections || []);
        setMemberships(userMemberships || []);
      })
      .catch((err: unknown) => {
        const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
        setError(msg ?? "Could not load laboratory sections or user memberships.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [open, user]);

  // Group sections by department
  const departmentGroups: DepartmentGroup[] = [];
  const groupMap = new Map<number, DepartmentGroup>();

  for (const sec of sections) {
    let group = groupMap.get(sec.departmentId);
    if (!group) {
      group = {
        departmentId: sec.departmentId,
        departmentName: sec.departmentName,
        departmentCode: sec.departmentCode,
        sections: []
      };
      groupMap.set(sec.departmentId, group);
      departmentGroups.push(group);
    }
    group.sections.push({
      sectionId: sec.sectionId,
      sectionName: sec.sectionName,
      sectionCode: sec.sectionCode
    });
  }

  const isWholeDepartmentChecked = (deptId: number) => {
    return memberships.some((m) => m.departmentId === deptId && m.sectionId === null);
  };

  const isSectionChecked = (deptId: number, secId: number) => {
    if (isWholeDepartmentChecked(deptId)) return true;
    return memberships.some((m) => m.departmentId === deptId && m.sectionId === secId);
  };

  const toggleWholeDepartment = (deptId: number) => {
    setMemberships((prev) => {
      const alreadyWhole = prev.some((m) => m.departmentId === deptId && m.sectionId === null);
      if (alreadyWhole) {
        return prev.filter((m) => !(m.departmentId === deptId && m.sectionId === null));
      } else {
        return [
          ...prev.filter((m) => m.departmentId !== deptId),
          { departmentId: deptId, sectionId: null }
        ];
      }
    });
  };

  const toggleSection = (deptId: number, secId: number) => {
    setMemberships((prev) => {
      const exists = prev.some((m) => m.departmentId === deptId && m.sectionId === secId);
      if (exists) {
        return prev.filter((m) => !(m.departmentId === deptId && m.sectionId === secId));
      } else {
        return [...prev, { departmentId: deptId, sectionId: secId }];
      }
    });
  };

  const handleSave = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    try {
      await replaceUserMemberships(user.id, memberships);
      onSuccess(`Laboratory sections updated for "${user.username}".`);
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "Could not update user memberships.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      title={`Laboratory Sections — ${user?.fullName ?? ""}`}
      actions={
        <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={saving || loading}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 3,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {saving ? "Saving..." : "Save Memberships"}
          </Button>
        </Box>
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        Assign laboratory sections to <strong>{user?.fullName} (@{user?.username})</strong>.
        Selecting the entire department grants access to all of its current and future sections.
      </Typography>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={32} />
        </Box>
      ) : departmentGroups.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No laboratory sections are configured in the system.
        </Typography>
      ) : (
        <Stack spacing={2}>
          {departmentGroups.map((dept) => {
            const wholeDeptChecked = isWholeDepartmentChecked(dept.departmentId);
            return (
              <Paper
                key={dept.departmentId}
                variant="outlined"
                sx={{
                  p: 2,
                  bgcolor: wholeDeptChecked ? "action.hover" : "background.paper",
                  borderColor: "divider",
                  borderRadius: 1.5
                }}
              >
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={wholeDeptChecked}
                      onChange={() => toggleWholeDepartment(dept.departmentId)}
                      color="primary"
                    />
                  }
                  label={
                    <Typography sx={{ fontWeight: 700, fontSize: 14 }}>
                      {dept.departmentName} ({dept.departmentCode}) — <em>All sections (entire department)</em>
                    </Typography>
                  }
                />

                <Divider sx={{ my: 1 }} />

                <Box
                  sx={{
                    pl: 3.5,
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                    gap: 0.5
                  }}
                >
                  {dept.sections.map((sec) => {
                    const secChecked = isSectionChecked(dept.departmentId, sec.sectionId);
                    return (
                      <FormControlLabel
                        key={sec.sectionId}
                        control={
                          <Checkbox
                            checked={secChecked}
                            disabled={wholeDeptChecked}
                            onChange={() => toggleSection(dept.departmentId, sec.sectionId)}
                            size="small"
                          />
                        }
                        label={
                          <Typography
                            sx={{
                              fontSize: 13,
                              color: wholeDeptChecked ? "text.secondary" : "text.primary"
                            }}
                          >
                            {sec.sectionName} ({sec.sectionCode})
                          </Typography>
                        }
                      />
                    );
                  })}
                </Box>
              </Paper>
            );
          })}
        </Stack>
      )}
    </FloatingDialog>
  );
}
