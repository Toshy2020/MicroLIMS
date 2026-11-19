import { useCallback, useEffect, useId, useMemo, useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { i18n, useTranslation } from "../../i18n";
import { useOptionalAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { type LaboratorySection, laboratorySectionService } from "../../services/laboratorySectionService";
import { getErrorMessage } from "../../utils/errorMessage";
import { getFieldErrors } from "../../utils/fieldErrors";
import { type MandatoryNotificationTypeDto, notificationService } from "./services/notificationService";

interface NotificationMandatoryPanelProps {
  onSaved?: () => void;
}

export function NotificationMandatoryPanel({ onSaved }: NotificationMandatoryPanelProps) {
  const { t } = useTranslation("notifications");
  const auth = useOptionalAuth();
  const canConfigure = Boolean(auth?.permissions.includes(PERMISSIONS.NOTIFICATIONS_CONFIGURE));
  const headingId = useId();
  const sectionLabelId = useId();

  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(false);
  const [sectionsError, setSectionsError] = useState<string | null>(null);
  const [sectionsReloadToken, setSectionsReloadToken] = useState(0);

  const [selectedSectionId, setSelectedSectionId] = useState<number | "">("");

  const [items, setItems] = useState<MandatoryNotificationTypeDto[] | null>(null);
  const [selectedTypes, setSelectedTypes] = useState<string[]>([]);
  const [loadingTypes, setLoadingTypes] = useState(false);
  const [typesError, setTypesError] = useState<string | null>(null);
  const [typesReloadToken, setTypesReloadToken] = useState(0);

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    if (!canConfigure) return;
    const controller = new AbortController();
    setSectionsLoading(true);
    setSectionsError(null);

    laboratorySectionService
      .getMySections(controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        const list = data ?? [];
        setSections(list);
        setSelectedSectionId((prev) => {
          if (typeof prev === "number" && list.some((s) => s.sectionId === prev)) {
            return prev;
          }
          return list.length > 0 ? list[0].sectionId : "";
        });
        setSectionsLoading(false);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setSectionsError(getErrorMessage(err, i18n.t("errors.sections", { ns: "notifications" })));
        setSectionsLoading(false);
      });

    return () => controller.abort();
  }, [canConfigure, sectionsReloadToken]);

  const loadSectionMandatory = useCallback((sectionId: number, signal: AbortSignal) => {
    setLoadingTypes(true);
    setTypesError(null);
    setSaveError(null);

    notificationService
      .getMandatory(sectionId, signal)
      .then((data) => {
        if (signal.aborted) return;
        setItems(data);
        setSelectedTypes(data.filter((item) => item.mandatory).map((item) => item.type));
        setLoadingTypes(false);
      })
      .catch((err: unknown) => {
        if (signal.aborted) return;
        setTypesError(getErrorMessage(err, i18n.t("errors.mandatoryTypes", { ns: "notifications" })));
        setLoadingTypes(false);
      });
  }, []);

  useEffect(() => {
    if (!canConfigure || typeof selectedSectionId !== "number") return;
    const controller = new AbortController();
    loadSectionMandatory(selectedSectionId, controller.signal);
    return () => controller.abort();
  }, [canConfigure, selectedSectionId, typesReloadToken, loadSectionMandatory]);

  const selectedSection = useMemo(
    () => sections.find((s) => s.sectionId === selectedSectionId) ?? null,
    [sections, selectedSectionId]
  );

  if (!canConfigure) {
    return null;
  }

  const toggleType = (type: string) => {
    setSelectedTypes((prev) =>
      prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type]
    );
    if (saveError) setSaveError(null);
  };

  const handleSave = async () => {
    if (typeof selectedSectionId !== "number" || saving) return;
    setSaving(true);
    setSaveError(null);

    try {
      const updated = await notificationService.putMandatory(selectedSectionId, selectedTypes);
      setItems(updated);
      setSelectedTypes(updated.filter((item) => item.mandatory).map((item) => item.type));
      toast.success(t("mandatory.saved"));
      onSaved?.();
    } catch (err: unknown) {
      const message = getErrorMessage(err, t("errors.saveMandatory"));
      const fieldErrors = getFieldErrors(err);
      const details = fieldErrors.map((fe) => fe.message).filter((msg) => msg.trim().length > 0);
      setSaveError(details.length > 0 ? Array.from(new Set(details)).join(" ") : message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Paper component="section" aria-labelledby={headingId} sx={{ p: 2.5, maxWidth: 760, mb: 2 }}>
      <Typography id={headingId} variant="h6" component="h3" sx={{ mb: 0.5 }}>
        {t("mandatory.title")}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {t("mandatory.intro", {
          reason: selectedSection
            ? t("mandatory.reasonQuoted", {
                reason: t("mandatory.forSection", { section: selectedSection.sectionName })
              })
            : t("mandatory.theirSectionName")
        })}
      </Typography>

      {sectionsLoading && (
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 1 }} role="status" aria-live="polite">
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">
            {t("loading.sections")}
          </Typography>
        </Stack>
      )}

      {!sectionsLoading && sectionsError && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => setSectionsReloadToken((n) => n + 1)}>
              {t("actions.retry")}
            </Button>
          }
        >
          {sectionsError}
        </Alert>
      )}

      {!sectionsLoading && !sectionsError && sections.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          {t("empty.noSections")}
        </Typography>
      )}

      {!sectionsLoading && !sectionsError && sections.length > 0 && (
        <>
          <FormControl size="small" sx={{ minWidth: 260, mb: 2 }}>
            <InputLabel id={sectionLabelId}>{t("mandatory.sectionField")}</InputLabel>
            <Select
              labelId={sectionLabelId}
              label={t("mandatory.sectionField")}
              value={selectedSectionId}
              disabled={saving}
              onChange={(event) => setSelectedSectionId(Number(event.target.value))}
            >
              {sections.map((section) => (
                <MenuItem key={section.sectionId} value={section.sectionId}>
                  {section.sectionName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {loadingTypes && (
            <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 1 }} role="status" aria-live="polite">
              <CircularProgress size={18} />
              <Typography variant="body2" color="text.secondary">
                {t("loading.types")}
              </Typography>
            </Stack>
          )}

          {!loadingTypes && typesError && (
            <Alert
              severity="error"
              action={
                <Button color="inherit" size="small" onClick={() => setTypesReloadToken((n) => n + 1)}>
                  {t("actions.retry")}
                </Button>
              }
            >
              {typesError}
            </Alert>
          )}

          {!loadingTypes && !typesError && items && items.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              {t("empty.noMandatoryTypes")}
            </Typography>
          )}

          {!loadingTypes && !typesError && items && items.length > 0 && (
            <>
              {saveError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {saveError}
                </Alert>
              )}

              <TableContainer>
                <Table size="small" aria-label={t("mandatory.tableLabel")}>
                  <TableHead>
                    <TableRow>
                      <TableCell>{t("mandatory.colType")}</TableCell>
                      <TableCell>{t("mandatory.colReason")}</TableCell>
                      <TableCell align="right">{t("mandatory.colMandatory")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {items.map((item) => {
                      const isMandatory = selectedTypes.includes(item.type);
                      return (
                        <TableRow key={item.type}>
                          <TableCell>
                            <Typography variant="body2" sx={{ fontWeight: 500 }}>
                              {item.title}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2" color="text.secondary">
                              {isMandatory && selectedSection
                                ? t("mandatory.forSection", { section: selectedSection.sectionName })
                                : t("mandatory.optional")}
                            </Typography>
                          </TableCell>
                          <TableCell align="right">
                            <Switch
                              checked={isMandatory}
                              disabled={saving}
                              onChange={() => toggleType(item.type)}
                              slotProps={{ input: { "aria-label": t("mandatory.switchLabel", { title: item.title }) } }}
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>

              <Stack direction="row" sx={{ justifyContent: "flex-end", mt: 2 }}>
                <Button
                  variant="contained"
                  onClick={handleSave}
                  disabled={saving || typeof selectedSectionId !== "number"}
                >
                  {saving ? t("actions.saving") : t("actions.saveMandatory")}
                </Button>
              </Stack>
            </>
          )}
        </>
      )}
    </Paper>
  );
}
