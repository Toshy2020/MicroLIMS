import { useEffect, useState } from "react";
import {
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
  Alert,
  Divider,
  TextField
} from "@mui/material";
import { Trans } from "react-i18next";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { i18n, useTranslation } from "../../../i18n";
import { UserRecord } from "../services/UserService";
import {
  getSections,
  getUserMemberships,
  replaceUserMemberships,
  LaboratorySection,
  UserMembership,
  PhyschemArea
} from "../../../services/laboratorySectionService";
import { brandColors } from "../../../theme";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "users", ...options });

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

function getErrorMessageVerbatim(err: unknown, fallback: string): string {
  const data = (err as { response?: { data?: unknown } })?.response?.data;
  if (typeof data === "string" && data.trim()) return data;
  const msg = (data as { message?: unknown } | undefined)?.message;
  if (typeof msg === "string" && msg.trim()) return msg;
  return (err as Error)?.message?.trim() ? (err as Error).message : fallback;
}

export function UserSectionsDialog({ open, onClose, user, onSuccess }: UserSectionsDialogProps) {
  const { t } = useTranslation("users");
  const { t: tc } = useTranslation("common");
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [memberships, setMemberships] = useState<UserMembership[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // A membership change decides what laboratory data the user can see and act on: the server requires a reason.
  const [reason, setReason] = useState("");

  useEffect(() => {
    setReason("");
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
        setError(getErrorMessageVerbatim(err, tl("sections.loadError")));
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
        return [...prev, { departmentId: deptId, sectionId: secId, physchemArea: null }];
      }
    });
  };

  const handleAreaChange = (deptId: number, secId: number, value: string) => {
    const area: PhyschemArea | null =
      value === "FinishedProduct" || value === "RawPackaging" ? value : null;
    setMemberships((prev) => {
      const exists = prev.some((m) => m.departmentId === deptId && m.sectionId === secId);
      if (exists) {
        return prev.map((m) =>
          m.departmentId === deptId && m.sectionId === secId
            ? { ...m, physchemArea: area }
            : m
        );
      }
      return [...prev, { departmentId: deptId, sectionId: secId, physchemArea: area }];
    });
  };

  const handleSave = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    try {
      const payload: UserMembership[] = memberships.map((m) => {
        const sec = sections.find((s) => s.sectionId === m.sectionId);
        const isFp = sec?.sectionCode?.toUpperCase() === "FP";
        return {
          departmentId: m.departmentId,
          sectionId: m.sectionId,
          physchemArea: isFp ? (m.physchemArea === "Both" ? null : m.physchemArea ?? null) : null
        };
      });
      await replaceUserMemberships(user.id, payload, reason.trim());
      onSuccess(t("sections.updateSuccess", { username: user.username }));
      onClose();
    } catch (err: unknown) {
      setError(getErrorMessageVerbatim(err, t("sections.updateError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      title={t("sections.title", { name: user?.fullName ?? "" })}
      actions={
        <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button onClick={onClose} disabled={saving} color="inherit">
            {tc("actions.cancel")}
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={saving || loading || !reason.trim()}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 3,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {saving ? t("sections.saving") : t("sections.save")}
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
        <Trans
          i18nKey="sections.intro"
          ns="users"
          values={{ fullName: user?.fullName ?? "", username: user?.username ?? "" }}
          components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
        />
      </Typography>

      <TextField
        label={t("fields.reasonForChange")}
        size="small"
        multiline
        rows={2}
        required
        fullWidth
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        placeholder={t("fields.reasonPlaceholder")}
        sx={{ mb: 2 }}
      />

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={32} />
        </Box>
      ) : departmentGroups.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("sections.empty")}
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
                      {dept.departmentName} ({dept.departmentCode}): <em>{t("sections.allSections")}</em>
                    </Typography>
                  }
                />

                <Divider sx={{ my: 1 }} />

                <Box
                  sx={{
                    pl: 3.5,
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                    gap: 1
                  }}
                >
                  {dept.sections.map((sec) => {
                    const secChecked = isSectionChecked(dept.departmentId, sec.sectionId);
                    const isFp = sec.sectionCode?.toUpperCase() === "FP";
                    const currentMembership = memberships.find(
                      (m) => m.departmentId === dept.departmentId && m.sectionId === sec.sectionId
                    );
                    const showAreaSelect = isFp && secChecked && !wholeDeptChecked;
                    const selectValue =
                      currentMembership?.physchemArea === "FinishedProduct"
                        ? "FinishedProduct"
                        : currentMembership?.physchemArea === "RawPackaging"
                          ? "RawPackaging"
                          : "Both";

                    return (
                      <Box
                        key={sec.sectionId}
                        sx={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          flexWrap: "wrap",
                          gap: 1,
                          py: 0.25
                        }}
                      >
                        <FormControlLabel
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
                          sx={{ m: 0 }}
                        />

                        {showAreaSelect && (
                          <FormControl size="small" sx={{ minWidth: 120 }}>
                            <InputLabel id={`area-select-label-${sec.sectionId}`}>{t("sections.area")}</InputLabel>
                            <Select
                              labelId={`area-select-label-${sec.sectionId}`}
                              id={`area-select-${sec.sectionId}`}
                              aria-label={t("sections.area")}
                              label={t("sections.area")}
                              value={selectValue}
                              onChange={(e) =>
                                handleAreaChange(dept.departmentId, sec.sectionId, e.target.value)
                              }
                              sx={{ fontSize: 13, height: 32 }}
                              data-testid="physchem-area-select"
                            >
                              <MenuItem value="FinishedProduct" sx={{ fontSize: 13 }}>
                                {t("sections.areaFp")}
                              </MenuItem>
                              <MenuItem value="RawPackaging" sx={{ fontSize: 13 }}>
                                {t("sections.areaRmPm")}
                              </MenuItem>
                              <MenuItem value="Both" sx={{ fontSize: 13 }}>
                                {t("sections.areaBoth")}
                              </MenuItem>
                            </Select>
                          </FormControl>
                        )}
                      </Box>
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
