import { useEffect, useId, useState } from "react";
import type { FormEvent } from "react";
import {
  Alert,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
} from "@mui/material";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import {
  laboratorySectionService,
  type LaboratorySection,
} from "../../../services/laboratorySectionService";
import { getErrorMessage } from "../../../utils/errorMessage";
import { errorsByField } from "../../../utils/fieldErrors";
import {
  tableViewService,
  type TableViewDto,
} from "./services/tableViewService";
import type { TableState } from "./tableUrlCodec";
import { serializeTableState } from "./viewState";

export type SaveViewMode = "create" | "copy" | "rename";

export interface SaveViewDialogProps {
  open: boolean;
  mode: SaveViewMode;
  tableKey: string;
  state: TableState;
  initialName?: string;
  viewToRename?: TableViewDto | null;
  onClose: () => void;
  onSaved: (view: TableViewDto, mode: SaveViewMode) => void;
}

const MAX_VIEW_NAME_LENGTH = 60;

export function SaveViewDialog({
  open,
  mode,
  tableKey,
  state,
  initialName = "",
  viewToRename,
  onClose,
  onSaved,
}: SaveViewDialogProps) {
  const { t } = useTranslation();
  const auth = useOptionalAuth();
  const canShare =
    (auth?.permissions.includes(PERMISSIONS.TABLE_VIEWS_SHARE) ?? false) &&
    mode !== "rename";

  const titleId = useId();
  const sectionLabelId = useId();

  const [name, setName] = useState(initialName);
  const [nameError, setNameError] = useState<string | null>(null);
  const [shareWithSection, setShareWithSection] = useState(false);
  const [sectionId, setSectionId] = useState<string>("");
  const [sectionError, setSectionError] = useState<string | null>(null);
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(initialName);
    setNameError(null);
    setShareWithSection(false);
    setSectionId("");
    setSectionError(null);
    setFormError(null);
  }, [open, initialName, mode]);

  useEffect(() => {
    if (!open || !canShare) return;
    const controller = new AbortController();
    setSectionsLoading(true);
    laboratorySectionService
      .getMySections(controller.signal)
      .then((list) => {
        if (controller.signal.aborted) return;
        setSections(list ?? []);
        setSectionsLoading(false);
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        setSections([]);
        setSectionsLoading(false);
      });
    return () => {
      controller.abort();
    };
  }, [open, canShare]);

  const title =
    mode === "rename"
      ? t("components.saveViewDialog.renameTitle")
      : mode === "copy"
        ? t("components.saveViewDialog.copyTitle")
        : t("components.saveViewDialog.createTitle");

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);

    const trimmed = name.trim();
    if (!trimmed) {
      setNameError(t("components.saveViewDialog.nameRequired"));
      return;
    }
    if (trimmed.length > MAX_VIEW_NAME_LENGTH) {
      setNameError(
        t("components.saveViewDialog.nameTooLong", { max: MAX_VIEW_NAME_LENGTH })
      );
      return;
    }

    if (canShare && shareWithSection && !sectionId) {
      setSectionError(t("components.saveViewDialog.sectionRequired"));
      return;
    }

    setSubmitting(true);
    try {
      let saved: TableViewDto;
      if (mode === "rename" && viewToRename) {
        saved = await tableViewService.update(viewToRename.id, {
          name: trimmed,
        });
      } else {
        saved = await tableViewService.create({
          tableKey,
          name: trimmed,
          stateJson: serializeTableState(state),
          scope: canShare && shareWithSection ? "Section" : "Personal",
          sectionId:
            canShare && shareWithSection && sectionId
              ? Number(sectionId)
              : null,
        });
      }
      setSubmitting(false);
      onSaved(saved, mode);
    } catch (err) {
      setSubmitting(false);
      const fieldErrs = errorsByField(err);
      const serverNameError = fieldErrs.name ?? fieldErrs.Name;
      if (serverNameError) {
        setNameError(serverNameError);
        return;
      }
      const serverSectionError = fieldErrs.sectionId ?? fieldErrs.SectionId;
      if (serverSectionError) {
        setSectionError(serverSectionError);
        return;
      }
      setFormError(getErrorMessage(err, t("components.saveViewDialog.saveFailed")));
    }
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      maxWidth="xs"
      fullWidth
      aria-labelledby={titleId}
    >
      <form onSubmit={handleSubmit} noValidate>
        <DialogTitle id={titleId}>{title}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {formError && <Alert severity="error">{formError}</Alert>}
            <TextField
              label={t("components.saveViewDialog.viewName")}
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                if (nameError) setNameError(null);
              }}
              error={Boolean(nameError)}
              helperText={nameError ?? undefined}
              fullWidth
              required
              autoFocus
              disabled={submitting}
            />
            {canShare && (
              <>
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={shareWithSection}
                      onChange={(e) => {
                        setShareWithSection(e.target.checked);
                        setSectionError(null);
                      }}
                      disabled={submitting}
                    />
                  }
                  label={t("components.saveViewDialog.shareWithSection")}
                />
                {shareWithSection && (
                  <FormControl
                    fullWidth
                    size="small"
                    error={Boolean(sectionError)}
                    disabled={submitting || sectionsLoading}
                  >
                    <InputLabel id={sectionLabelId}>{t("components.saveViewDialog.section")}</InputLabel>
                    <Select
                      labelId={sectionLabelId}
                      label={t("components.saveViewDialog.section")}
                      value={sectionId}
                      onChange={(e) => {
                        setSectionId(String(e.target.value));
                        setSectionError(null);
                      }}
                    >
                      {sections.map((s) => (
                        <MenuItem key={s.sectionId} value={String(s.sectionId)}>
                          {s.sectionName}
                        </MenuItem>
                      ))}
                    </Select>
                    {sectionError && (
                      <FormHelperText>{sectionError}</FormHelperText>
                    )}
                  </FormControl>
                )}
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={submitting}>
            {t("actions.cancel")}
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={submitting}
            startIcon={
              submitting ? (
                <CircularProgress size={16} color="inherit" />
              ) : undefined
            }
          >
            {t("actions.save")}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
