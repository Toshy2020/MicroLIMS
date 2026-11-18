import { useState, useEffect } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography
} from "@mui/material";
import { Trans } from "react-i18next";
import { DateField } from "../../../components/DateField";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { serverMessage } from "../../../utils/errorMessage";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { isDuplicateCalendarEvent } from "../eventDuplicates";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EVENT_TYPE_KEY } from "../statusDisplay";
import type { CalendarEventResponse, CalendarEventType, CreateCalendarEventRequest } from "../types";

export interface AddEventDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
  initialDate?: string | null;
  initialMonth?: string | null;
  onSubmit?: (data: CreateCalendarEventRequest) => Promise<void> | void;
  existingEvents?: CalendarEventResponse[];
  events?: CalendarEventResponse[];
}

export function AddEventDialog({
  open,
  onClose,
  onCreated,
  initialDate,
  initialMonth,
  onSubmit,
  existingEvents,
  events
}: AddEventDialogProps) {
  const { t } = useTranslation("monitoring");
  const [type, setType] = useState<CalendarEventType>("VendorVisit");
  const [title, setTitle] = useState("");
  const [dateMode, setDateMode] = useState<"day" | "month">(
    initialMonth && !initialDate ? "month" : "day"
  );
  const [date, setDate] = useState("");
  const [month, setMonth] = useState("");
  const [note, setNote] = useState("");
  const [equipmentList, setEquipmentList] = useState<EquipmentItem[]>([]);
  const [selectedEquipment, setSelectedEquipment] = useState<EquipmentItem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [duplicateInfo, setDuplicateInfo] = useState<{ title: string; type: CalendarEventType; dateOrMonth: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      setType("VendorVisit");
      setTitle("");
      setDate(initialDate ?? "");
      setMonth(initialMonth ?? "");
      setDateMode(initialMonth && !initialDate ? "month" : "day");
      setSelectedEquipment(null);
      setNote("");
      setError(null);
      setDuplicateInfo(null);
      setBusy(false);
      EquipmentInventoryService.getAll()
        .then((items) => setEquipmentList(items || []))
        .catch(() => setEquipmentList([]));
    }
  }, [open, initialDate, initialMonth]);

  const submitWithData = async (confirmDuplicate = false) => {
    if (!title.trim()) {
      setError(t("errors.titleRequired"));
      return;
    }

    const targetDate = dateMode === "day" && date ? date : null;
    const targetMonth = dateMode === "month" && month ? month : null;
    const eqId = selectedEquipment ? selectedEquipment.id : null;

    if (!confirmDuplicate) {
      const isDup = (existingEvents ?? events ?? []).some((e) =>
        isDuplicateCalendarEvent(e, {
          type,
          title: title.trim(),
          date: targetDate,
          month: targetMonth,
          equipmentInventoryId: eqId
        })
      );
      if (isDup) {
        setError(null);
        setDuplicateInfo({ title: title.trim(), type, dateOrMonth: targetDate ?? targetMonth ?? "" });
        return;
      }
    }

    setBusy(true);
    setError(null);
    setDuplicateInfo(null);

    const payload: CreateCalendarEventRequest = {
      type,
      title: title.trim(),
      date: targetDate,
      month: targetMonth,
      note: note.trim() || null,
      equipmentInventoryId: eqId,
      ...(confirmDuplicate ? { confirmDuplicate: true } : {})
    };

    try {
      if (onSubmit) {
        await onSubmit(payload);
      } else {
        await EquipmentPlanService.createEvent(payload);
      }
      onCreated?.();
      onClose();
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? (err instanceof Error ? err.message : "");
      if (typeof msg === "string" && msg.startsWith("A matching event already exists")) {
        setDuplicateInfo({ title: title.trim(), type, dateOrMonth: targetDate ?? targetMonth ?? "" });
      } else {
        setError(msg || t("errors.createEvent"));
      }
    } finally {
      setBusy(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    void submitWithData(false);
  };

  const handleAddAnyway = () => {
    void submitWithData(true);
  };

  const handleBack = () => {
    setDuplicateInfo(null);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>{t("eventDialog.addTitle")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}
            {duplicateInfo && (
              <Alert severity="warning">
                <Box>
                  <Typography variant="body2" sx={{ mb: 1 }}>
                    <Trans
                      i18nKey="eventDialog.duplicateWarning"
                      ns="monitoring"
                      values={{
                        title: duplicateInfo.title,
                        type: t(EVENT_TYPE_KEY[duplicateInfo.type]),
                        dateOrMonth: duplicateInfo.dateOrMonth
                      }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  </Typography>
                  <Box sx={{ display: "flex", gap: 1 }}>
                    <Button
                      size="small"
                      variant="contained"
                      onClick={handleAddAnyway}
                      disabled={busy}
                    >
                      {t("commonFields.addAnyway")}
                    </Button>
                    <Button
                      size="small"
                      onClick={handleBack}
                      disabled={busy}
                    >
                      {t("commonFields.back")}
                    </Button>
                  </Box>
                </Box>
              </Alert>
            )}

            <FormControl fullWidth size="small">
              <InputLabel id="event-type-label">{t("eventDialog.eventType")}</InputLabel>
              <Select
                labelId="event-type-label"
                value={type}
                label={t("eventDialog.eventType")}
                onChange={(e) => setType(e.target.value as CalendarEventType)}
              >
                {(Object.keys(EVENT_TYPE_KEY) as CalendarEventType[]).map((k) => (
                  <MenuItem key={k} value={k}>
                    {t(EVENT_TYPE_KEY[k])}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              label={t("eventDialog.title")}
              required
              fullWidth
              size="small"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder={t("eventDialog.titlePlaceholder")}
            />

            <Box>
              <ToggleButtonGroup
                value={dateMode}
                exclusive
                size="small"
                onChange={(_, val) => {
                  if (val) {
                    setDateMode(val);
                    if (val === "day") {
                      setMonth("");
                    } else {
                      setDate("");
                    }
                  }
                }}
              >
                <ToggleButton value="day" sx={{ textTransform: "none" }}>
                  {t("eventDialog.onADay")}
                </ToggleButton>
                <ToggleButton value="month" sx={{ textTransform: "none" }}>
                  {t("eventDialog.wholeMonth")}
                </ToggleButton>
              </ToggleButtonGroup>
            </Box>

            {dateMode === "day" ? (
              <DateField
                label={t("eventDialog.specificDate")}
                fullWidth
                value={date}
                onChange={(v) => setDate(v)}
              />
            ) : (
              <TextField
                label={t("eventDialog.wholeMonth")}
                type="month"
                size="small"
                fullWidth
                value={month}
                onChange={(e) => setMonth(e.target.value)}
                slotProps={{ inputLabel: { shrink: true } }}
              />
            )}

            <Autocomplete
              size="small"
              options={equipmentList}
              value={selectedEquipment}
              onChange={(_, val) => setSelectedEquipment(val)}
              getOptionLabel={(o) => `${o.code} — ${o.instrumentType}`}
              isOptionEqualToValue={(o, v) => o.id === v.id}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label={t("eventDialog.instrumentOptional")}
                  placeholder={t("eventDialog.instrumentPlaceholder")}
                />
              )}
            />

            <TextField
              label={t("eventDialog.note")}
              multiline
              rows={3}
              fullWidth
              size="small"
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder={t("eventDialog.notePlaceholder")}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={busy}>
            {t("commonFields.cancel")}
          </Button>
          <Button type="submit" variant="contained" disabled={busy || !title.trim()}>
            {t("eventDialog.addEvent")}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
