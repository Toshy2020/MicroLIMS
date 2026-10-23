import { useState, useEffect } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  Link,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EVENT_TYPE_LABEL } from "../statusDisplay";
import type { CalendarEventResponse, CalendarEventType, UpdateCalendarEventRequest } from "../types";

export interface EventDialogProps {
  event: CalendarEventResponse | null;
  open: boolean;
  onClose: () => void;
  onChanged: () => void;
}

export function EventDialog({
  event,
  open,
  onClose,
  onChanged
}: EventDialogProps) {
  const [currentEvent, setCurrentEvent] = useState<CalendarEventResponse | null>(event);
  const [type, setType] = useState<CalendarEventType>("VendorVisit");
  const [title, setTitle] = useState("");
  const [date, setDate] = useState("");
  const [month, setMonth] = useState("");
  const [note, setNote] = useState("");

  const [showCancel, setShowCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setCurrentEvent(event);
    if (event) {
      setType(event.type);
      setTitle(event.title);
      setDate(event.date ?? "");
      setMonth(event.month ?? "");
      setNote(event.note ?? "");
      setShowCancel(false);
      setCancelReason("");
      setError(null);
      setBusy(false);
    }
  }, [event]);

  if (!currentEvent) return null;

  const isCancelled = currentEvent.status === "Cancelled";
  const hasDocuments = currentEvent.documents && currentEvent.documents.length > 0;
  const isPtWithoutDocs = currentEvent.type === "ProficiencyTesting" && (!hasDocuments || currentEvent.reportPending);

  const handleSave = async () => {
    if (!title.trim()) {
      setError("Title is required.");
      return;
    }
    setBusy(true);
    setError(null);
    const body: UpdateCalendarEventRequest = {
      type,
      title: title.trim(),
      date: date ? date : null,
      month: month && !date ? month : null,
      note: note.trim() || null,
      equipmentInventoryId: currentEvent.equipmentInventoryId
    };

    try {
      const updated = await EquipmentPlanService.updateEvent(currentEvent.id, currentEvent.version, body);
      setCurrentEvent(updated);
      onChanged();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not update the event.");
    } finally {
      setBusy(false);
    }
  };

  const handleCancelEvent = async () => {
    if (!cancelReason.trim()) {
      setError("A cancellation reason is required.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const updated = await EquipmentPlanService.cancelEvent(currentEvent.id, cancelReason.trim());
      setCurrentEvent(updated);
      setShowCancel(false);
      onChanged();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not cancel the event.");
    } finally {
      setBusy(false);
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const updated = await EquipmentPlanService.uploadEventDocument(currentEvent.id, file);
      setCurrentEvent(updated);
      onChanged();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not upload the report.");
    } finally {
      setBusy(false);
      e.target.value = "";
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 1 }}>
        <Typography variant="h6" component="span" sx={{ fontWeight: 600 }}>
          {isCancelled ? "Cancelled event" : "Event details"}
        </Typography>
        <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
          {isPtWithoutDocs && (
            <Chip label="Report pending" color="warning" size="small" />
          )}
          {isCancelled && (
            <Chip label="Cancelled" color="error" size="small" />
          )}
        </Box>
      </DialogTitle>

      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}

          {currentEvent.equipmentCode && (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Instrument: <strong>{currentEvent.equipmentCode}</strong>
            </Typography>
          )}

          <FormControl fullWidth size="small" disabled={isCancelled}>
            <InputLabel id="edit-event-type-label">Event type</InputLabel>
            <Select
              labelId="edit-event-type-label"
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
            disabled={isCancelled}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              label="Specific date"
              type="date"
              size="small"
              disabled={isCancelled}
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
              disabled={isCancelled}
              value={month}
              onChange={(e) => {
                setMonth(e.target.value);
                if (e.target.value) setDate("");
              }}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Box>

          <TextField
            label="Note"
            multiline
            rows={2}
            fullWidth
            size="small"
            disabled={isCancelled}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />

          <Divider sx={{ my: 1 }} />

          {/* Documents / Report section */}
          <Box>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>Report & attachments</Typography>
              {!isCancelled && (
                <Button
                  component="label"
                  size="small"
                  variant="outlined"
                  startIcon={<CloudUploadIcon />}
                  disabled={busy}
                >
                  Attach report
                  <input type="file" hidden onChange={handleFileUpload} />
                </Button>
              )}
            </Box>

            {currentEvent.documents && currentEvent.documents.length > 0 ? (
              <Stack spacing={1}>
                {currentEvent.documents.map((doc) => (
                  <Box
                    key={doc.id}
                    sx={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      p: 1,
                      border: 1,
                      borderColor: "divider",
                      borderRadius: 1
                    }}
                  >
                    <Link
                      href={EquipmentPlanService.getDocumentContentUrl(doc.id)}
                      target="_blank"
                      rel="noopener noreferrer"
                      underline="hover"
                      sx={{ fontSize: 13, fontWeight: 500 }}
                    >
                      {doc.originalFileName}
                    </Link>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {Math.round(doc.fileSizeBytes / 1024)} KB · {doc.uploadedByName}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            ) : (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                No report attached yet.
              </Typography>
            )}
          </Box>

          {/* Cancellation section */}
          {!isCancelled && (
            <>
              <Divider sx={{ my: 1 }} />
              {!showCancel ? (
                <Box>
                  <Button
                    color="error"
                    size="small"
                    onClick={() => setShowCancel(true)}
                    disabled={busy}
                  >
                    Cancel event
                  </Button>
                </Box>
              ) : (
                <Stack spacing={1.5} sx={{ p: 1.5, border: 1, borderColor: "error.light", borderRadius: 1 }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 600, color: "error.main" }}>
                    Cancel this event
                  </Typography>
                  <TextField
                    label="Reason for cancellation"
                    required
                    size="small"
                    fullWidth
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                  />
                  <Box sx={{ display: "flex", gap: 1 }}>
                    <Button
                      variant="contained"
                      color="error"
                      size="small"
                      disabled={busy || !cancelReason.trim()}
                      onClick={handleCancelEvent}
                    >
                      Confirm cancellation
                    </Button>
                    <Button size="small" onClick={() => setShowCancel(false)}>
                      Back
                    </Button>
                  </Box>
                </Stack>
              )}
            </>
          )}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Close
        </Button>
        {!isCancelled && (
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={busy || !title.trim()}
          >
            Save changes
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
