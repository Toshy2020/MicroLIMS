import { useEffect, useMemo, useState } from "react";
import { Alert, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { useTranslation } from "../../../i18n";
import { apiClient } from "../../../services/apiClient";
import { laboratorySectionService } from "../../../services/laboratorySectionService";
import { serverMessage } from "../../../utils/errorMessage";
import { MonitoringPlanService } from "../services/MonitoringPlanService";

interface Machine { id: number; name: string }
interface WaterPoint { id: number; code?: string; location?: string; name?: string; assignedTestCodes?: string[] }

// An occurrence can be entered by hand:
// 1. After Cleaning: pick the machine and when it is due (a day, any day in a week,
//    or any day in a month). It is for the machine itself; parts are chosen at preparation.
// 2. Water: pick the water sampling point, the lab section, and when it is due.
export function ManualEntryDialog({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: () => void }) {
  const { t } = useTranslation("monitoring");
  const { options } = useTestDefinitions();
  const [targetType, setTargetType] = useState<"Machine" | "Water">("Machine");
  const [machines, setMachines] = useState<Machine[]>([]);
  const [machineId, setMachineId] = useState<number | "">("");
  const [waterPoints, setWaterPoints] = useState<WaterPoint[]>([]);
  const [waterPointId, setWaterPointId] = useState<number | "">("");
  const [labSectionId, setLabSectionId] = useState<number | "">("");
  const [sections, setSections] = useState<{ id: number; code: string; name?: string }[]>([]);

  const [kind, setKind] = useState<"Day" | "Week" | "Month">("Day");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setTargetType("Machine");
    setMachineId("");
    setWaterPointId("");
    setLabSectionId("");
    setKind("Day");
    setDate("");
    setReason("");
    apiClient.get("/masterdata/machines").then((r) => setMachines(r.data.data)).catch(() => setError(t("errors.loadMachines")));
    apiClient.get("/masterdata/water-sampling-points").then((r) => setWaterPoints(r.data.data)).catch(() => {});
    laboratorySectionService
      .getSections()
      .then((list) => {
        if (Array.isArray(list)) {
          setSections(list.map((s) => ({ id: s.sectionId, code: s.sectionCode, name: s.sectionName })));
        }
      })
      .catch(() => {});
  }, [open, t]);

  const availableLabs = useMemo(() => {
    if (targetType !== "Water") return [];
    const selectedPoint = waterPoints.find((p) => p.id === waterPointId);
    const map = new Map<number, { id: number; code: string; name?: string }>();
    if (selectedPoint?.assignedTestCodes?.length) {
      for (const code of selectedPoint.assignedTestCodes) {
        const def = options.find((o) => o.code === code);
        const secId = def?.section?.id ?? def?.sectionId;
        const secCode = def?.section?.code ?? (secId === 1 ? "MICRO" : secId === 2 ? "FP" : "");
        if (secId != null) {
          map.set(secId, { id: secId, code: secCode || t("manualEntry.sectionFallback", { id: secId }), name: def?.section?.name });
        }
      }
    }
    if (map.size > 0) return Array.from(map.values());
    if (sections.length > 0) return sections;
    return [
      { id: 1, code: "MICRO", name: t("manualEntry.microbiology") },
      { id: 2, code: "FP", name: t("manualEntry.physicochemical") }
    ];
  }, [targetType, waterPointId, waterPoints, options, sections, t]);

  const isFormValid =
    Boolean(date) &&
    (targetType === "Machine"
      ? machineId !== ""
      : waterPointId !== "" && labSectionId !== "");

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const targetDate = kind === "Month" ? `${date}-01` : date;
      if (targetType === "Water") {
        await MonitoringPlanService.createManual({
          waterSamplingPointId: Number(waterPointId),
          labSectionId: Number(labSectionId),
          targetDate,
          kind,
          reason: reason || undefined
        });
      } else {
        await MonitoringPlanService.createManual({
          machineId: Number(machineId),
          targetDate,
          kind,
          reason: reason || undefined
        });
      }
      onCreated();
      onClose();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("errors.createOccurrence"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      title={targetType === "Water" ? t("manualEntry.titleWater") : t("manualEntry.titleAfterCleaning")}
      onClose={onClose}
      maxWidth="sm"
      actions={
        <>
          <Button onClick={onClose} disabled={busy}>{t("commonFields.cancel")}</Button>
          <Button variant="contained" onClick={create} disabled={busy || !isFormValid}>{t("manualEntry.add")}</Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          select
          label={t("manualEntry.targetType")}
          value={targetType}
          onChange={(e) => {
            setTargetType(e.target.value as "Machine" | "Water");
            setError(null);
          }}
        >
          <MenuItem value="Machine">{t("manualEntry.targetMachine")}</MenuItem>
          <MenuItem value="Water">{t("manualEntry.targetWater")}</MenuItem>
        </TextField>

        {targetType === "Machine" ? (
          <>
            <TextField select label={t("manualEntry.machine")} value={machineId} onChange={(e) => setMachineId(Number(e.target.value))}>
              {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
            </TextField>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("manualEntry.machineHint")}
            </Typography>
          </>
        ) : (
          <>
            <TextField select label={t("manualEntry.waterPoint")} value={waterPointId} onChange={(e) => setWaterPointId(Number(e.target.value))}>
              {waterPoints.map((p) => (
                <MenuItem key={p.id} value={p.id}>
                  {p.code ? (p.location ? <><Ltr>{p.code}</Ltr> – {p.location}</> : <Ltr>{p.code}</Ltr>) : p.name || t("manualEntry.pointFallback", { id: p.id })}
                </MenuItem>
              ))}
            </TextField>
            <TextField select label={t("manualEntry.lab")} value={labSectionId} onChange={(e) => setLabSectionId(Number(e.target.value))}>
              {availableLabs.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.code ? <Ltr>{s.code}</Ltr> : s.name}
                </MenuItem>
              ))}
            </TextField>
          </>
        )}

        <TextField select label={t("manualEntry.due")} value={kind} onChange={(e) => { setKind(e.target.value as "Day" | "Week" | "Month"); setDate(""); }}>
          <MenuItem value="Day">{t("manualEntry.dueDay")}</MenuItem>
          <MenuItem value="Week">{t("manualEntry.dueWeek")}</MenuItem>
          <MenuItem value="Month">{t("manualEntry.dueMonth")}</MenuItem>
        </TextField>
        <TextField
          label={kind === "Day" ? t("manualEntry.labelDay") : kind === "Week" ? t("manualEntry.labelWeek") : t("manualEntry.labelMonth")}
          type={kind === "Month" ? "month" : "date"}
          slotProps={{ inputLabel: { shrink: true } }}
          value={date}
          onChange={(e) => setDate(e.target.value)}
          helperText={kind === "Week" ? t("manualEntry.helperWeek") : kind === "Month" ? t("manualEntry.helperMonth") : t("manualEntry.helperDay")}
        />
        <TextField label={t("manualEntry.reasonOptional")} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Stack>
    </FloatingDialog>
  );
}
