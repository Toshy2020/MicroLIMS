import { useState, useEffect } from "react";
import {
  Alert,
  Autocomplete,
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
  ToggleButton,
  ToggleButtonGroup,
  Typography
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { EVENT_TYPE_LABEL } from "../statusDisplay";
import type { CalendarEventResponse, CalendarEventType, UpdateCalendarEventRequest } from "../types";
import { isDuplicateCalendarEvent } from "../eventDuplicates";
import { DateField } from "../../../components/DateField";

export interface EventDialogProps {
  event: CalendarEventResponse | null;
  open: boolean;
  onClose: () => void;
  onChanged: () => void;
  existingEvents?: CalendarEventResponse[];
  events?: CalendarEventResponse[];
  onSubmit?: (data: UpdateCalendarEventRequest) => Promise<void> | void;
}

export function EventDialog({
  event,
  open,
  onClose,
  onChanged,
  existingEvents,
  events,
  onSubmit
}: EventDialogProps) {
  const [currentEvent, setCurrentEvent] = useState<CalendarEventResponse | null>(event);
  const [type, setType] = useState<CalendarEventType>("VendorVisit");
  const [title, setTitle] = useState("");
  const [dateMode, setDateMode] = useState<"day" | "month">("day");
  const [date, setDate] = useState("");
  const [month, setMonth] = useState("");
  const [note, setNote] = useState("");
  const [equipmentList, setEquipmentList] = useState<EquipmentItem[]>([]);
  const [selectedEquipment, setSelectedEquipment] = useState<EquipmentItem | null>(null);

  const [showCancel, setShowCancel] = useState(false);
  const [cancelReason, setCancelReason] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [duplicateWarning, setDuplicateWarning] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      EquipmentInventoryService.getAll()
        .then((items) => setEquipmentList(items || []))
        .catch(() => setEquipmentList([]));
    }
  }, [open]);

  useEffect(() => {
    setCurrentEvent(event);
    if (event) {
      setType(event.type);
      setTitle(event.title);
      setDate(event.date ?? "");
      setMonth(event.month ?? "");
      setDateMode(event.month && !event.date ? "month" : "day");
      setNote(event.note ?? "");
      setShowCancel(false);
      setCancelReason("");
      setError(null);
      setDuplicateWarning(null);
      setBusy(false);

      if (event.equipmentInventoryId) {
        setSelectedEquipment({
          id: event.equipmentInventoryId,
          code: event.equipmentCode ?? "",
          instrumentType: ""
        } as EquipmentItem);
      } else {
        setSelectedEquipment(null);
      }
    }
  }, [event]);

  useEffect(() => {
    if (currentEvent?.equipmentInventoryId && equipmentList.length > 0) {
      const match = equipmentList.find((e) => e.id === currentEvent.equipmentInventoryId);
      if (match) {
        setSelectedEquipment((prev) => (prev?.id === match.id ? match : prev));
      }
    }
  }, [equipmentList, currentEvent?.equipmentInventoryId]);

  if (!currentEvent) return null;

  const isCancelled = currentEvent.status === "Cancelled";
  const hasDocuments = currentEvent.documents && currentEvent.documents.length > 0;
  const isPtWithoutDocs = currentEvent.type === "ProficiencyTesting" && (!hasDocuments || currentEvent.reportPending);

  const handleSave = async (confirmDuplicateInput: boolean | React.MouseEvent = false) => {
    const confirmDuplicate = typeof confirmDuplicateInput === "boolean" ? confirmDuplicateInput : false;
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
          id: currentEvent.id,
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
        setDuplicateWarning(`An event "${title.trim()}" (${EVENT_TYPE_LABEL[type]}) is already on ${dateOrMonth}.`);
        return;
      }
    }

    setBusy(true);
    setError(null);
    setDuplicateWarning(null);
    const body: UpdateCalendarEventRequest = {
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
        await onSubmit(body);
      } else {
        const updated = await EquipmentPlanService.updateEvent(currentEvent.id, currentEvent.version, body);
        setCurrentEvent(updated);
      }
      onChanged();
      onClose();
    } catch (err: any) {
      const msg = err?.response?.data?.message ?? err?.message ?? "";
      if (typeof msg === "string" && msg.startsWith("A matching event already exists")) {
        const dateOrMonth = targetDate ?? targetMonth ?? "";
        setDuplicateWarning(`An event "${title.trim()}" (${EVENT_TYPE_LABEL[type]}) is already on ${dateOrMonth}.`);
      } else {
        setError(msg || "Could not update the event.");
      }
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
                    onClick={() => void handleSave(true)}
                    disabled={busy}
                  >
                    Add anyway
                  </Button>
                  <Button
                    size="small"
                    onClick={() => setDuplicateWarning(null)}
                    disabled={busy}
                  >
                    Back
                  </Button>
                </Box>
              </Box>
            </Alert>
          )}

          {(currentEvent.equipmentCode || selectedEquipment?.code) && (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Instrument: <strong>{selectedEquipment?.code ?? currentEvent.equipmentCode}</strong>
            </Typography>
          )}

          <Autocomplete
            size="small"
            disabled={isCancelled}
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

          <Box>
            <ToggleButtonGroup
              value={dateMode}
              exclusive
              size="small"
              disabled={isCancelled}
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
            <DateField
              label="Specific date"
              fullWidth
              disabled={isCancelled}
              value={date}
              onChange={(v) => setDate(v)}
            />
          ) : (
            <TextField
              label="Whole month"
              type="month"
              size="small"
              fullWidth
              disabled={isCancelled}
              value={month}
              onChange={(e) => setMonth(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          )}

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
            onClick={() => void handleSave(false)}
            disabled={busy || !title.trim()}
          >
            Save changes
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
