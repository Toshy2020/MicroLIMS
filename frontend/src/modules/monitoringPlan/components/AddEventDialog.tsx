import { useState, useEffect } from "react";
import {
  Alert,
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
  TextField
} from "@mui/material";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EVENT_TYPE_LABEL } from "../statusDisplay";
import type { CalendarEventType, CreateCalendarEventRequest } from "../types";

export interface AddEventDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
  initialDate?: string | null;
  initialMonth?: string | null;
  onSubmit?: (data: CreateCalendarEventRequest) => Promise<void> | void;
}

export function AddEventDialog({
  open,
  onClose,
  onCreated,
  initialDate,
  initialMonth,
  onSubmit
}: AddEventDialogProps) {
  const [type, setType] = useState<CalendarEventType>("VendorVisit");
  const [title, setTitle] = useState("");
  const [date, setDate] = useState("");
  const [month, setMonth] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      setType("VendorVisit");
      setTitle("");
      setDate(initialDate ?? "");
      setMonth(initialMonth ?? "");
      setNote("");
      setError(null);
      setBusy(false);
    }
  }, [open, initialDate, initialMonth]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setError("Title is required.");
      return;
    }

    setBusy(true);
    setError(null);
    const payload: CreateCalendarEventRequest = {
      type,
      title: title.trim(),
      date: date ? date : null,
      month: month && !date ? month : null,
      note: note.trim() || null
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
      setError(err?.response?.data?.message ?? "Could not create the event.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>Add calendar event</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}

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

            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
              <TextField
                label="Specific date"
                type="date"
                size="small"
                value={date}
                onChange={(e) => {
                  setDate(e.target.value);
                  if (e.target.value) setMonth("");
                }}
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                label="Whole month"
                type="month"
                size="small"
                value={month}
                onChange={(e) => {
                  setMonth(e.target.value);
                  if (e.target.value) setDate("");
                }}
                slotProps={{ inputLabel: { shrink: true } }}
                helperText="Use date OR month"
              />
            </Box>

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
