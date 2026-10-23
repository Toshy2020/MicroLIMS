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
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { EVENT_TYPE_LABEL } from "../statusDisplay";
import type { CalendarEventResponse, CalendarEventType, CreateCalendarEventRequest } from "../types";
import { isDuplicateCalendarEvent } from "../eventDuplicates";

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
  const [duplicateWarning, setDuplicateWarning] = useState<string | null>(null);
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
      setDuplicateWarning(null);
      setBusy(false);
      EquipmentInventoryService.getAll()
        .then((items) => setEquipmentList(items || []))
        .catch(() => setEquipmentList([]));
    }
  }, [open, initialDate, initialMonth]);

  const submitWithData = async (confirmDuplicate = false) => {
    if (!title.trim()) {
      setError("Title is required.");
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
        const dateOrMonth = targetDate ?? targetMonth ?? "";
        setError(null);
        setDuplicateWarning(`A ${type} called "${title.trim()}" is already on ${dateOrMonth}.`);
        return;
      }
    }

    setBusy(true);
    setError(null);
    setDuplicateWarning(null);

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
    } catch (err: any) {
      const msg = err?.response?.data?.message ?? err?.message ?? "";
      if (typeof msg === "string" && msg.startsWith("A matching event already exists")) {
        const dateOrMonth = targetDate ?? targetMonth ?? "";
        setDuplicateWarning(`A ${type} called "${title.trim()}" is already on ${dateOrMonth}.`);
      } else {
        setError(msg || "Could not create the event.");
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
    setDuplicateWarning(null);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>Add calendar event</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}
            {duplicateWarning && (
              <Alert severity="warning">
                <Box>
                  <Typography variant="body2" sx={{ mb: 1 }}>
                    {duplicateWarning}
                  </Typography>
                  <Box sx={{ display: "flex", gap: 1 }}>
                    <Button
                      size="small"
                      variant="contained"
                      onClick={handleAddAnyway}
                      disabled={busy}
                    >
                      Add anyway
                    </Button>
                    <Button
                      size="small"
                      onClick={handleBack}
                      disabled={busy}
                    >
                      Back
                    </Button>
                  </Box>
                </Box>
              </Alert>
            )}

            <FormControl fullWidth size="small">
              <InputLabel id="event-type-label">Event type</InputLabel>
              <Select
                labelId="event-type-label"
                value={type}
                label="Event type"
                onChange={(e) => setType(e.target.value as CalendarEventType)}
              >
                {(Object.keys(EVENT_TYPE_LABEL) as CalendarEventType[]).map((t) => (
                  <MenuItem key={t} value={t}>
                    {EVENT_TYPE_LABEL[t]}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              label="Title"
              required
              fullWidth
              size="small"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="e.g. Annual balance calibration"
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
                  On a day
                </ToggleButton>
                <ToggleButton value="month" sx={{ textTransform: "none" }}>
                  Whole month
                </ToggleButton>
              </ToggleButtonGroup>
            </Box>

            {dateMode === "day" ? (
              <TextField
                label="Specific date"
                type="date"
                size="small"
                fullWidth
                value={date}
                onChange={(e) => setDate(e.target.value)}
                slotProps={{ inputLabel: { shrink: true } }}
              />
            ) : (
              <TextField
                label="Whole month"
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
                  label="Instrument (optional)"
                  placeholder="Search equipment register..."
                />
              )}
            />

            <TextField
              label="Note"
              multiline
              rows={3}
              fullWidth
              size="small"
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Optional notes or details"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={busy || !title.trim()}>
            Add event
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
