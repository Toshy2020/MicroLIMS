import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  CircularProgress,
  Alert,
  FormControl,
  InputLabel,
  Select,
  MenuItem
} from "@mui/material";
import { PrintButton } from "../../../../components/PrintButton";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { formatLabDate, formatLabDateTime } from "../../../../utils/formatDate";
import type {
  InstrumentHistoryResponse,
  EquipmentTaskStatus
} from "../types/equipmentTypes";
import {
  EQUIPMENT_STATUS_LABELS,
  MAINTENANCE_ENTRY_TYPE_LABELS
} from "../types/equipmentTypes";

interface Props {
  history?: InstrumentHistoryResponse | null;
  equipmentId?: number;
  year?: number;
}

const TASK_STATUS_COLORS: Record<EquipmentTaskStatus, "default" | "success" | "warning" | "error" | "info"> = {
  Upcoming: "default",
  Due: "warning",
  Overdue: "error",
  Done: "success",
  Retired: "default"
};

export function EquipmentHistoryPrint({
  history: propHistory,
  equipmentId,
  year: propYear
}: Props) {
  const currentYear = new Date().getFullYear();
  const [selectedYear, setSelectedYear] = useState<number>(propYear ?? currentYear);
  const [history, setHistory] = useState<InstrumentHistoryResponse | null>(propHistory ?? null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (propHistory) {
      setHistory(propHistory);
      return;
    }

    if (equipmentId) {
      setLoading(true);
      setError(null);
      EquipmentPlanService.getInstrumentHistory(equipmentId, selectedYear)
        .then(setHistory)
        .catch(() => setError("Failed to load instrument history."))
        .finally(() => setLoading(false));
    }
  }, [propHistory, equipmentId, selectedYear]);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 5 }}>
        <CircularProgress size={28} />
      </Box>
    );
  }

  if (error) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  if (!history) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography sx={{ fontStyle: "italic", color: "text.secondary" }}>
          No instrument history available.
        </Typography>
      </Box>
    );
  }

  const years = [currentYear - 2, currentYear - 1, currentYear, currentYear + 1];

  return (
    <Box
      sx={{
        p: { xs: 2, sm: 3 },
        color: "text.primary",
        fontFamily: "'Segoe UI', Roboto, Helvetica, Arial, sans-serif"
      }}
    >
      {/* Action Toolbar (no-print) */}
      <Box
        className="no-print"
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          mb: 2.5,
          gap: 2,
          flexWrap: "wrap"
        }}
      >
        {!propHistory && equipmentId && (
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel id="history-year-select-label">Year</InputLabel>
            <Select
              labelId="history-year-select-label"
              id="history-year-select"
              value={selectedYear}
              label="Year"
              onChange={(e) => setSelectedYear(Number(e.target.value))}
            >
              {years.map((y) => (
                <MenuItem key={y} value={y}>
                  {y}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}
        <Box sx={{ ml: "auto" }}>
          <PrintButton label="Print History" />
        </Box>
      </Box>

      {/* Printable Report Header */}
      <Box sx={{ mb: 2.5, pb: 2, borderBottom: 2, borderColor: "text.secondary" }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 0.5 }}>
          <Typography sx={{ fontSize: "16pt", fontWeight: 800, color: "text.primary", letterSpacing: "-0.5px" }}>
            MicroLIMS
          </Typography>
          <Typography sx={{ fontSize: "9pt", color: "text.secondary" }}>
            Printed: {new Date().toLocaleString()}
          </Typography>
        </Box>
        <Typography sx={{ fontSize: "13pt", fontWeight: 700, color: "text.primary", mb: 0.25 }}>
          Equipment History & Maintenance Record
        </Typography>
        <Typography sx={{ fontSize: "9.5pt", color: "text.secondary" }}>
          Approved plan, actual task executions, evidence, reschedules, status changes, and logbook entries.
        </Typography>
      </Box>

      {/* Instrument Overview Card */}
      <Box
        sx={{
          bgcolor: "action.hover",
          border: 1, borderColor: "divider",
          borderRadius: 1.5,
          p: 2,
          mb: 3
        }}
      >
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
            gap: 1.5,
            fontSize: "9.5pt"
          }}
        >
          <Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Equipment Code:</strong> {history.equipmentCode}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Instrument Type:</strong> {history.instrumentType}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Manufacturer:</strong> {history.manufacturerName || "—"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Serial Number:</strong> {history.serialNumber || "—"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Location:</strong> {history.location}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Status:</strong> {EQUIPMENT_STATUS_LABELS[history.status] ?? history.status}
            </Box>
          </Box>
          <Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Calibration Interval:</strong> {history.calibrationIntervalDescription || "None"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>PM Interval:</strong> {history.pmIntervalDescription || "None"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Next Calibration Due:</strong>{" "}
              {history.nextCalibrationDue ? formatLabDate(history.nextCalibrationDue) : "—"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Next PM Due:</strong>{" "}
              {history.nextPmDue ? formatLabDate(history.nextPmDue) : "—"}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>Calibration Baseline:</strong>{" "}
              {history.calibrationBaseline ? (
                <span>
                  {formatLabDate(history.calibrationBaseline.date)}{" "}
                  <Chip
                    label={history.calibrationBaseline.label || "Baseline"}
                    size="small"
                    sx={{ height: 18, fontSize: "8pt", bgcolor: "action.selected", color: "primary.main" }}
                  />
                </span>
              ) : (
                "—"
              )}
            </Box>
            <Box sx={{ py: 0.25 }}>
              <strong>PM Baseline:</strong>{" "}
              {history.pmBaseline ? (
                <span>
                  {formatLabDate(history.pmBaseline.date)}{" "}
                  <Chip
                    label={history.pmBaseline.label || "Baseline"}
                    size="small"
                    sx={{ height: 18, fontSize: "8pt", bgcolor: "action.selected", color: "primary.main" }}
                  />
                </span>
              ) : (
                "—"
              )}
            </Box>
          </Box>
        </Box>
      </Box>

      {/* SECTION 1 — Planned vs Done Tasks */}
      <Box sx={{ mb: 3 }}>
        <Typography sx={{ fontSize: "11pt", fontWeight: 700, color: "text.primary", mb: 1, textTransform: "uppercase" }}>
          Planned vs Actual Tasks
        </Typography>
        {history.tasks.length === 0 ? (
          <Typography sx={{ fontSize: "9pt", color: "text.secondary", fontStyle: "italic" }}>
            No planned tasks recorded for this period.
          </Typography>
        ) : (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead sx={{ bgcolor: "action.hover" }}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Task Type</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Schedule & Completion</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Status</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Evidence / Certificate</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.tasks.map((t) => {
                  const scheduleLine = t.completedOn
                    ? `Planned ${t.originalDueDate} · Done ${t.completedOn}`
                    : `Planned ${t.originalDueDate} · Due ${t.dueDate}`;
                  return (
                    <TableRow key={t.id}>
                      <TableCell sx={{ fontSize: "9pt", fontWeight: 500 }}>
                        {t.type === "Calibration" ? "Calibration" : "Preventive Maintenance"}
                      </TableCell>
                      <TableCell sx={{ fontSize: "9pt" }}>
                        <strong>{scheduleLine}</strong>
                      </TableCell>
                      <TableCell sx={{ fontSize: "9pt" }}>
                        <Chip
                          label={t.status}
                          size="small"
                          color={TASK_STATUS_COLORS[t.status] ?? "default"}
                          sx={{ fontSize: "8pt", height: 20 }}
                        />
                      </TableCell>
                      <TableCell sx={{ fontSize: "9pt", color: "text.secondary" }}>
                        {t.evidenceLabel || "—"}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>

      {/* SECTION 2 — Task Reschedules */}
      <Box sx={{ mb: 3 }}>
        <Typography sx={{ fontSize: "11pt", fontWeight: 700, color: "text.primary", mb: 1, textTransform: "uppercase" }}>
          Task Reschedules
        </Typography>
        {history.reschedules.length === 0 ? (
          <Typography sx={{ fontSize: "9pt", color: "text.secondary", fontStyle: "italic" }}>
            No task reschedules recorded.
          </Typography>
        ) : (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead sx={{ bgcolor: "action.hover" }}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Task Type</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Previous Due</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>New Due</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Reason</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Performed By</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Date</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.reschedules.map((r, i) => (
                  <TableRow key={i}>
                    <TableCell sx={{ fontSize: "9pt" }}>{r.taskType}</TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{r.oldValue || "—"}</TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{r.newValue || "—"}</TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{r.reason || "—"}</TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {r.performedByName || (r.performedByUserId ? `User #${r.performedByUserId}` : "—")}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{formatLabDateTime(r.performedAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>

      {/* SECTION 3 — Operational Status Changes */}
      <Box sx={{ mb: 3 }}>
        <Typography sx={{ fontSize: "11pt", fontWeight: 700, color: "text.primary", mb: 1, textTransform: "uppercase" }}>
          Operational Status History
        </Typography>
        {history.statusChanges.length === 0 ? (
          <Typography sx={{ fontSize: "9pt", color: "text.secondary", fontStyle: "italic" }}>
            No operational status changes recorded.
          </Typography>
        ) : (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead sx={{ bgcolor: "action.hover" }}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Change</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Comment / Reason</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Changed By</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Date & Time</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.statusChanges.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {EQUIPMENT_STATUS_LABELS[s.previousStatus] ?? s.previousStatus} →{" "}
                      <strong>{EQUIPMENT_STATUS_LABELS[s.newStatus] ?? s.newStatus}</strong>
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{s.comment}</TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {s.changedByName || `User #${s.changedByUserId}`}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{formatLabDateTime(s.changedAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>

      {/* SECTION 4 — Maintenance Logbook Entries */}
      <Box sx={{ mb: 2 }}>
        <Typography sx={{ fontSize: "11pt", fontWeight: 700, color: "text.primary", mb: 1, textTransform: "uppercase" }}>
          Maintenance Logbook Entries
        </Typography>
        {history.logbook.length === 0 ? (
          <Typography sx={{ fontSize: "9pt", color: "text.secondary", fontStyle: "italic" }}>
            No maintenance logbook entries recorded for this period.
          </Typography>
        ) : (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead sx={{ bgcolor: "action.hover" }}>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Date</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Type</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Work Done</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Parts Replaced</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Findings</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Performed By</TableCell>
                  <TableCell sx={{ fontWeight: 700, fontSize: "8.5pt" }}>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.logbook.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell sx={{ fontSize: "9pt", whiteSpace: "nowrap" }}>
                      {formatLabDate(e.performedOn)}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {MAINTENANCE_ENTRY_TYPE_LABELS[e.entryType] ?? e.entryType}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>{e.workDone}</TableCell>
                    <TableCell sx={{ fontSize: "9pt", color: "text.secondary" }}>
                      {e.partsReplaced || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt", color: "text.secondary" }}>
                      {e.findings || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {e.performedByUserName || `User #${e.performedByUserId}`}
                    </TableCell>
                    <TableCell sx={{ fontSize: "9pt" }}>
                      {e.voidedAt ? (
                        <Chip
                          label="Voided"
                          color="error"
                          size="small"
                          sx={{ fontSize: "8pt", height: 18 }}
                        />
                      ) : (
                        <Chip
                          label="Recorded"
                          color="success"
                          variant="outlined"
                          size="small"
                          sx={{ fontSize: "8pt", height: 18 }}
                        />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>
    </Box>
  );
}
