import { useEffect, useState } from "react";
import {
  Alert,
  Autocomplete,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography
} from "@mui/material";
import { reassignmentService } from "../services/reassignmentService";
import { getErrorMessage } from "../../../utils/errorMessage";
import type { ReassignmentCandidate, ReassignmentItem } from "../types/reassignmentTypes";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";

interface ReassignDialogProps {
  item: ReassignmentItem | null;
  onClose: () => void;
  onReassigned: (item: ReassignmentItem, newUser: ReassignmentCandidate) => void;
}

// Hand a deactivated user's responsibility to someone else. The list is what the server would accept (an active
// member of the document's department whose role holds the needed permission and who passes the segregation of
// duties rules); the server checks all of it again on save.
export function ReassignDialog({ item, onClose, onReassigned }: ReassignDialogProps) {
  const { t } = useTranslation("documentControl");
  const [candidates, setCandidates] = useState<ReassignmentCandidate[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<ReassignmentCandidate | null>(null);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelected(null);
    setReason("");
    setError(null);
    setCandidates([]);
    if (!item) return;
    let live = true;
    setLoading(true);
    reassignmentService
      .getCandidates(item.itemType, item.itemId)
      .then((list) => { if (live) setCandidates(list); })
      .catch((e) => { if (live) setError(getErrorMessage(e, t("reassignDialog.loadError"))); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [item, t]);

  const canSave = !!item && !!selected && reason.trim().length > 0 && !saving;

  const save = async () => {
    if (!item || !selected) return;
    setSaving(true);
    setError(null);
    try {
      await reassignmentService.reassign({ itemType: item.itemType, itemId: item.itemId, newUserId: selected.userId, reason: reason.trim() });
      onReassigned(item, selected);
    } catch (e) {
      setError(getErrorMessage(e, t("reassignDialog.reassignError")));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!item} onClose={saving ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t("reassignDialog.title", { role: item?.roleNeeded })}</DialogTitle>
      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        {item && (
          <Typography variant="body2">
            <strong>{item.documentCode}</strong> &ndash; {item.documentTitle}
            {item.revisionNumber ? t("reassignDialog.revisionSuffix", { rev: item.revisionNumber }) : ""}
            <br />
            <Trans
              i18nKey="reassignDialog.description"
              ns="documentControl"
              values={{
                holder: item.holderName ?? t("reassignDialog.nameNotShown"),
                department: item.departmentName,
                permission: item.requiredPermission
              }}
              components={{ strong: <strong />, code: <code /> }}
            />
          </Typography>
        )}
        {error && <Alert severity="error">{error}</Alert>}
        {loading ? (
          <CircularProgress size={24} aria-label={t("reassignDialog.loadingAria")} />
        ) : candidates.length === 0 && !error ? (
          <Alert severity="warning">
            {t("reassignDialog.noCandidates", { department: item?.departmentName, permission: item?.requiredPermission })}
          </Alert>
        ) : (
          <Autocomplete
            options={candidates}
            value={selected}
            onChange={(_, value) => setSelected(value)}
            getOptionLabel={(c) => c.fullName}
            renderOption={(props, c) => (
              <li {...props} key={c.userId}>
                {c.fullName}
                <Typography component="span" variant="caption" sx={{ ml: 1, color: "text.secondary" }}>
                  {[c.jobTitle, c.roleName].filter(Boolean).join(" · ")}
                </Typography>
              </li>
            )}
            renderInput={(params) => <TextField {...params} label={t("reassignDialog.newHolderLabel")} required size="small" />}
          />
        )}
        <TextField
          label={t("reassignDialog.reasonLabel")}
          size="small"
          multiline
          rows={2}
          required
          fullWidth
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder={t("reassignDialog.reasonPlaceholder")}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving} color="inherit">{t("common.cancel")}</Button>
        <Button variant="contained" onClick={save} disabled={!canSave}>
          {saving ? t("reassignDialog.reassigning") : t("reassignDialog.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
