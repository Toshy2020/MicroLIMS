import { useEffect, useMemo, useRef, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  Alert,
  TextField,
  Select,
  MenuItem,
  Divider,
  Stack,
  Stepper,
  Step,
  StepButton,
  FormControl,
  FormHelperText,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { MediaPreparationService } from "../services/MediaPreparationService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import {
  EquipmentConfigurationService,
  AutoclaveProgram,
  EquipmentResponse
} from "../../masterDataSimple/services/EquipmentConfigurationService";
import type { MediaLot } from "../../mediaEvaluation/types/mediaEvaluationTypes";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../../hooks/useLoadFailures";
import { DateField } from "../../../../components/DateField";
import { ScanField } from "../../../../components/labels/ScanField";
import { NumericEntryField } from "../../../../components/bench/NumericEntryField";
import { BenchDialog } from "../../../../components/bench/BenchDialog";
import { DraftRestoreNotice } from "../../../../components/bench/DraftRestoreNotice";
import { useBenchDraft } from "../../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, useKeptOffline } from "../../../testingWorkspace/benchSave";
import { getErrorMessage } from "../../../../utils/errorMessage";
import { extractServerFieldErrors } from "../../../../utils/serverFieldErrors";
import { FieldHint, hintDescribedBy } from "../../../../components/FieldHint";
import { hints } from "../hints";
import { MediaAdditivesSection, UsableSolution } from "../components/MediaAdditivesSection";
import { AdditiveRow, buildAdditivesPayload, validateAdditives } from "../components/mediaAdditives";

interface MediaPreparationFormState {
  materialId?: number | "";
  totalWeight?: string;
  totalVolume?: string;
  autoclaveEquipmentId?: number | "";
  autoclaveProgramId?: number | "";
  autoclaveProgram?: string;
  loadType?: string;
  temperature?: number | "";
  cycleTime?: number | "";
  cycleNumber?: string;
  ph?: string;
  expiryDate?: string;
}

interface MediaDraftValues extends MediaPreparationFormState {
  additives?: AdditiveRow[];
}

// Explicit allow-list of fields stored in the tablet draft.
function pickMediaDraft(v: MediaDraftValues): Partial<MediaDraftValues> {
  return {
    materialId: v.materialId,
    totalWeight: v.totalWeight,
    totalVolume: v.totalVolume,
    autoclaveEquipmentId: v.autoclaveEquipmentId,
    autoclaveProgramId: v.autoclaveProgramId,
    autoclaveProgram: v.autoclaveProgram,
    loadType: v.loadType,
    temperature: v.temperature,
    cycleTime: v.cycleTime,
    cycleNumber: v.cycleNumber,
    ph: v.ph,
    expiryDate: v.expiryDate,
    additives: (v.additives ?? []).map((a) => ({
      key: a.key,
      kind: a.kind,
      sourceId: a.sourceId,
      quantity: a.quantity,
      unit: a.unit
    }))
  };
}

type MediaFieldKey =
  | "materialId"
  | "totalWeight"
  | "totalVolume"
  | "autoclaveEquipmentId"
  | "autoclaveProgram"
  | "cycleNumber"
  | "ph"
  | "expiryDate";

const MEDIA_FIELD_MAP: Record<string, MediaFieldKey> = {
  materialid: "materialId",
  totalweight: "totalWeight",
  totalvolume: "totalVolume",
  autoclaveequipmentid: "autoclaveEquipmentId",
  autoclaveprogram: "autoclaveProgram",
  cyclenumber: "cycleNumber",
  ph: "ph",
  expirydate: "expiryDate"
};

function matchMediaField(text: string): MediaFieldKey | null {
  if (/\bweight\b/i.test(text)) return "totalWeight";
  if (/\bvolume\b/i.test(text)) return "totalVolume";
  if (/\bcycle\s*number\b/i.test(text)) return "cycleNumber";
  if (/\bph\b/i.test(text)) return "ph";
  if (/\bexpiry/i.test(text)) return "expiryDate";
  if (/\bprogram\b/i.test(text)) return "autoclaveProgram";
  if (/\bautoclave\b/i.test(text)) return "autoclaveEquipmentId";
  if (/\bmaterial|dehydrated/i.test(text)) return "materialId";
  return null;
}

const BENCH_SECTIONS = [
  "Preparation Details",
  "Sterilization & Autoclave",
  "Quality & Expiry",
  "Additives"
];

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (newLot: MediaLot) => void;
}

export function MediaPreparationDialog({ open, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const { benchMode } = useBenchMode();
  const [autoclaves, setAutoclaves] = useState<EquipmentResponse[]>([]);
  const [dehydratedMedia, setDehydratedMedia] = useState<MaterialItem[]>([]);
  const [autoclavePrograms, setAutoclavePrograms] = useState<AutoclaveProgram[]>([]);
  const [form, setForm] = useState<MediaPreparationFormState>({});
  const [additives, setAdditives] = useState<AdditiveRow[]>([]);
  const [allStock, setAllStock] = useState<MaterialItem[]>([]);
  const [solutions, setSolutions] = useState<UsableSolution[]>([]);
  const additiveKey = useRef(0);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<MediaFieldKey, string>>>({});
  const [saving, setSaving] = useState(false);
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [activeSection, setActiveSection] = useState(0);
  const sectionRefs = useRef<(HTMLDivElement | null)[]>([]);
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();

  useEffect(() => {
    if (open) {
      setError(null);
      setFieldErrors({});
      setKeptOffline(false);
      setActiveSection(0);
      setForm({});
      setAdditives([]);
      setAutoclavePrograms([]);
      resetFailures();
      masterDataOptions.getEquipment("Autoclave").then(setAutoclaves).catch(fail("autoclaves", () => setAutoclaves([])));
      MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia).catch(fail("dehydrated media stock", () => setDehydratedMedia([])));
      MaterialService.getAll().then(setAllStock).catch(fail("additive stock lots", () => setAllStock([])));
    }
  }, [open, fail, resetFailures, setKeptOffline]);

  const usableStock = dehydratedMedia.filter((m) => m.status === "InStock");
  const selectedMaterial = usableStock.find((m) => m.id === form.materialId);

  const sectionId: number | undefined = selectedMaterial?.sectionId;

  const draftValues = useMemo<MediaDraftValues>(() => ({ ...form, additives }), [form, additives]);
  const draft = useBenchDraft<MediaDraftValues>({
    formKey: "media-preparation",
    recordKey: "new",
    recordVersion: null,
    title: selectedMaterial ? `Prepare ${selectedMaterial.materialName}` : "Prepare Media Lot",
    values: draftValues,
    pick: pickMediaDraft,
    enabled: benchMode && open
  });

  const handleRestore = () => {
    const restored = draft.acceptRestore();
    if (!restored) return;
    const { additives: restoredAdditives, ...restoredForm } = restored;
    setForm(restoredForm);
    if (Array.isArray(restoredAdditives)) {
      const maxKey = restoredAdditives.reduce((m, r) => Math.max(m, r.key), 0);
      additiveKey.current = Math.max(additiveKey.current, maxKey);
      setAdditives(restoredAdditives);
    }
    if (restoredForm.autoclaveEquipmentId) {
      EquipmentConfigurationService.getAutoclavePrograms(Number(restoredForm.autoclaveEquipmentId), true)
        .then((programs) => setAutoclavePrograms(Array.isArray(programs) ? programs : []))
        .catch(() => setAutoclavePrograms([]));
    }
  };

  const goToSection = (index: number) => {
    setActiveSection(index);
    sectionRefs.current[index]?.scrollIntoView?.({ behavior: "smooth", block: "start" });
  };

  useEffect(() => {
    if (!open) return;
    MediaPreparationService.getUsableSolutions(sectionId).then(setSolutions).catch(fail("prepared solutions", () => setSolutions([])));
  }, [open, sectionId, fail]);

  // Additive stock lots: usable lots of the same laboratory, never the media or microorganism lots themselves.
  const additiveStock = allStock.filter(
    (m) =>
      m.status === "InStock" &&
      m.materialType !== "DehydratedMedia" &&
      m.materialType !== "LyophilizedMicroorganism" &&
      m.approvalStatus !== "PendingReview" &&
      m.approvalStatus !== "Rejected" &&
      (sectionId == null || m.sectionId === sectionId)
  );

  const setField = <K extends keyof MediaPreparationFormState>(
    k: K,
    v: MediaPreparationFormState[K]
  ) => {
    setForm((f) => ({ ...f, [k]: v }));
    if (k in MEDIA_FIELD_MAP || k === "materialId" || k === "totalWeight" || k === "totalVolume" || k === "autoclaveEquipmentId" || k === "autoclaveProgram" || k === "cycleNumber" || k === "ph" || k === "expiryDate") {
      const fk = k as MediaFieldKey;
      setFieldErrors((prev) => (prev[fk] ? { ...prev, [fk]: undefined } : prev));
    }
  };

  const handleAutoclaveChange = async (eqId: number | "") => {
    setField("autoclaveEquipmentId", eqId);
    setField("autoclaveProgramId", "");
    setField("autoclaveProgram", "");
    setField("loadType", "");
    setField("temperature", "");
    setField("cycleTime", "");
    setAutoclavePrograms([]);

    if (eqId) {
      try {
        const programs = await EquipmentConfigurationService.getAutoclavePrograms(Number(eqId), true);
        setAutoclavePrograms(Array.isArray(programs) ? programs : []);
      } catch {
        setAutoclavePrograms([]);
      }
    }
  };

  const handleProgramChange = (progId: number | "") => {
    const prog = autoclavePrograms.find((p) => p.id === Number(progId));
    if (prog) {
      setForm((f) => ({
        ...f,
        autoclaveProgramId: prog.id,
        autoclaveProgram: `${prog.programCode}: ${prog.programName}`,
        loadType: prog.loadType,
        temperature: prog.temperature,
        cycleTime: prog.cycleTimeMinutes
      }));
    } else {
      setForm((f) => ({
        ...f,
        autoclaveProgramId: "",
        autoclaveProgram: "",
        loadType: "",
        temperature: "",
        cycleTime: ""
      }));
    }
  };

  const submitPreparation = async () => {
    setError(null);
    setFieldErrors({});
    setKeptOffline(false);

    // Basic required field validation
    if (!form.materialId) {
      setError("Please select a Dehydrated Media Stock item from inventory.");
      return;
    }
    if (selectedMaterial && selectedMaterial.mediaProductId == null) {
      setError("The selected dehydrated media is not linked to a media product. Edit it in Inventory > Materials Stock before preparing.");
      return;
    }
    if (!form.totalWeight) {
      setError("Please enter the total weight.");
      return;
    }
    if (!form.totalVolume) {
      setError("Please enter the total volume.");
      return;
    }
    if (!form.autoclaveEquipmentId) {
      setError("Please select an Autoclave.");
      return;
    }
    if (!form.autoclaveProgram) {
      setError("Please select an Autoclave Program / Load.");
      return;
    }
    if (!form.loadType) {
      setError("Please select an Autoclave Program with valid Load Type.");
      return;
    }
    if (!form.temperature) {
      setError("Autoclave temperature is required.");
      return;
    }
    if (!form.cycleTime) {
      setError("Autoclave cycle time is required.");
      return;
    }
    if (!form.cycleNumber) {
      setError("Please specify the cycle number.");
      return;
    }
    if (!form.ph) {
      setError("Please specify the pH.");
      return;
    }
    if (!form.expiryDate) {
      setError("Please specify the Expiry Date.");
      return;
    }

    const additiveError = validateAdditives(additives);
    if (additiveError) {
      setError(additiveError);
      return;
    }

    setSaving(true);
    try {
      const result = await MediaPreparationService.prepare(
        {
          materialId: Number(form.materialId),
          totalWeight: Number(form.totalWeight),
          totalVolume: form.totalVolume,
          autoclaveEquipmentId: Number(form.autoclaveEquipmentId),
          autoclaveProgram: form.autoclaveProgram,
          loadType: form.loadType,
          temperature: Number(form.temperature),
          cycleTime: Number(form.cycleTime),
          cycleNumber: Number(form.cycleNumber),
          ph: Number(form.ph),
          expiryDate: form.expiryDate,
          additives: buildAdditivesPayload(additives)
        },
        ...draftHeaderArgs(draft.submitHeaders())
      );

      await draft.onSubmitted();
      onSuccess(result);
      onClose();
    } catch (err: unknown) {
      const failure = classifySaveFailure(err);
      if (benchMode && failure.kind === "network") {
        setKeptOffline(true);
        return;
      }
      if (benchMode && failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
        return;
      }
      const nextFieldErrors = extractServerFieldErrors(err, MEDIA_FIELD_MAP, matchMediaField);
      if (Object.keys(nextFieldErrors).length > 0) {
        setFieldErrors(nextFieldErrors);
      }
      setError(getErrorMessage(err, "Could not prepare media lot. Please verify all inputs."));
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    void submitPreparation();
  };

  const isDirty = Boolean(
    form.materialId ||
    form.totalWeight ||
    form.totalVolume ||
    form.autoclaveEquipmentId ||
    form.cycleNumber ||
    form.ph ||
    form.expiryDate ||
    additives.length > 0
  );

  const sectionWrapperSx = benchMode
    ? { minHeight: "calc(100vh - 220px)", display: "flex", flexDirection: "column" as const, justifyContent: "flex-start" as const }
    : undefined;

  const dialogTitle = (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <ScienceOutlinedIcon sx={{ color: theme.palette.primary.main }} />
        <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main }}>
          Prepare New Media Lot
        </Typography>
      </Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
        Record formulation, autoclave sterilization parameters, pH, and expiration for the new media lot.
      </Typography>
    </Box>
  );

  const formBody = (
    <>
      <LoadFailuresAlert failed={failedLists} sx={{ mb: 2 }} />
      {benchMode && (
        <DraftRestoreNotice
          startedAt={draft.restoredStartedAt}
          onRestore={handleRestore}
          onDiscard={() => void draft.discard()}
        />
      )}
      {benchMode && keptOffline && (
        <OfflineDraftNotice onTryAgain={() => void submitPreparation()} />
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {benchMode && (
        <Stepper
          nonLinear
          activeStep={activeSection}
          aria-label="Media preparation steps"
          sx={{ mb: 2 }}
          data-testid="media-step-indicator"
        >
          {BENCH_SECTIONS.map((label, index) => (
            <Step key={label}>
              <StepButton onClick={() => goToSection(index)}>{label}</StepButton>
            </Step>
          ))}
        </Stepper>
      )}

      <Stack spacing={2.5}>
        {/* Section 1: Preparation Details */}
        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[0] = el; }}
          data-bench-section="0"
          sx={sectionWrapperSx}
        >
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
            1. PREPARATION DETAILS & INVENTORY STOCK
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, alignItems: "start" }}>
            <Stack spacing={1}>
              <ScanField
                label="Scan dehydrated media stock lot"
                expectedType="MaterialStockLot"
                offeredIds={usableStock.map((m) => m.id)}
                onSelect={(id) => setField("materialId", id)}
                disabled={saving}
              />
              <FormControl fullWidth size="small" error={Boolean(fieldErrors.materialId)}>
                <Select
                  size="small"
                  displayEmpty
                  fullWidth
                  value={form.materialId ?? ""}
                  onChange={(e) => setField("materialId", e.target.value as number | "")}
                  inputProps={{ "aria-label": "Dehydrated Media Stock (Inventory)" }}
                >
                  <MenuItem value="">
                    <em>Dehydrated Media Stock (Inventory) *</em>
                  </MenuItem>
                  {usableStock.map((m) => {
                    const codePrefix = m.code ? `${m.code}: ` : "";
                    const label = m.mediaProductId == null
                      ? `${codePrefix}${m.materialName}, batch ${m.batchNumber} (not linked to a media product - edit it in Inventory > Materials Stock)`
                      : `${codePrefix}${m.materialName}, batch ${m.batchNumber} (${m.quantityRemaining} ${m.unit} left)`;

                    return (
                      <MenuItem key={m.id} value={m.id} disabled={m.mediaProductId == null}>
                        {label}
                      </MenuItem>
                    );
                  })}
                </Select>
                {fieldErrors.materialId && <FormHelperText>{fieldErrors.materialId}</FormHelperText>}
              </FormControl>
              {selectedMaterial && (
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    display: "block",
                    mt: 0.5
                  }}>
                  Manufacturer: {selectedMaterial.manufacturerName} · Batch: {selectedMaterial.batchNumber}
                </Typography>
              )}
            </Stack>

            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
              <NumericEntryField
                label="Total Weight (g) *"
                kind="decimal"
                value={form.totalWeight ?? ""}
                onChange={(v) => setField("totalWeight", v)}
                unit="g"
                error={fieldErrors.totalWeight}
                aria-describedby={hintDescribedBy("media-total-weight")}
              />
              <FieldHint id="media-total-weight" label="Total Weight" hint={hints.totalWeight} />
            </Stack>

            <TextField
              size="small"
              label="Total Volume *"
              placeholder="e.g. 1000 mL or 2 L"
              value={form.totalVolume ?? ""}
              onChange={(e) => setField("totalVolume", e.target.value)}
              error={Boolean(fieldErrors.totalVolume)}
              helperText={fieldErrors.totalVolume}
            />
          </Box>
        </Box>

        <Divider />

        {/* Section 2: Sterilization */}
        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[1] = el; }}
          data-bench-section="1"
          sx={sectionWrapperSx}
        >
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
            2. STERILIZATION & AUTOCLAVE PARAMETERS
          </Typography>
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              mb: 1.5
            }}>
            Select an autoclave and configured program. Load Type, Temperature, and Cycle Time are automatically set from Laboratory Configuration.
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, alignItems: "start" }}>
            <Stack spacing={1}>
              <ScanField
                label="Scan autoclave"
                expectedType="EquipmentAsset"
                offeredIds={autoclaves.map((a) => a.id)}
                onSelect={(id) => {
                  void handleAutoclaveChange(id);
                }}
                disabled={saving}
              />
              <FormControl fullWidth size="small" error={Boolean(fieldErrors.autoclaveEquipmentId)}>
                <Select
                  size="small"
                  displayEmpty
                  fullWidth
                  value={form.autoclaveEquipmentId ?? ""}
                  onChange={(e) => {
                    void handleAutoclaveChange(e.target.value as number | "");
                  }}
                  inputProps={{ "aria-label": "Autoclave" }}
                >
                  <MenuItem value="">
                    <em>Select Autoclave *</em>
                  </MenuItem>
                  {autoclaves.map((a) => (
                    <MenuItem key={a.id} value={a.id}>
                      {a.code}: {a.name}
                    </MenuItem>
                  ))}
                </Select>
                {fieldErrors.autoclaveEquipmentId && <FormHelperText>{fieldErrors.autoclaveEquipmentId}</FormHelperText>}
              </FormControl>
            </Stack>

            <FormControl fullWidth size="small" error={Boolean(fieldErrors.autoclaveProgram)}>
              <Select
                size="small"
                displayEmpty
                disabled={!form.autoclaveEquipmentId || !Array.isArray(autoclavePrograms) || autoclavePrograms.length === 0}
                value={form.autoclaveProgramId ?? ""}
                onChange={(e) => handleProgramChange(e.target.value as number | "")}
                inputProps={{ "aria-label": "Autoclave program" }}
              >
                <MenuItem value="">
                  <em>{!Array.isArray(autoclavePrograms) || autoclavePrograms.length === 0 ? "No active programs for autoclave" : "Select Program / Load *"}</em>
                </MenuItem>
                {Array.isArray(autoclavePrograms) && autoclavePrograms.map((p) => (
                  <MenuItem key={p.id} value={p.id}>
                    {p.programCode}: {p.programName} ({p.temperature}°C, {p.cycleTimeMinutes} min)
                  </MenuItem>
                ))}
              </Select>
              {fieldErrors.autoclaveProgram && <FormHelperText>{fieldErrors.autoclaveProgram}</FormHelperText>}
            </FormControl>

            <TextField
              size="small"
              label="Load Type (Read-Only)"
              value={form.loadType ?? ""}
              helperText="Configured in Laboratory Configuration"
              slotProps={{
                input: { readOnly: true }
              }}
            />

            <NumericEntryField
              label="Temperature (°C) (Read-Only)"
              kind="decimal"
              value={form.temperature === "" || form.temperature == null ? "" : String(form.temperature)}
              onChange={() => {}}
              readOnly
              unit="°C"
              helperText="Configured in Laboratory Configuration"
            />

            <NumericEntryField
              label="Cycle Time (min) (Read-Only)"
              kind="decimal"
              value={form.cycleTime === "" || form.cycleTime == null ? "" : String(form.cycleTime)}
              onChange={() => {}}
              readOnly
              unit="min"
              helperText="Configured in Laboratory Configuration"
            />

            <NumericEntryField
              label="Cycle Number *"
              kind="count"
              value={form.cycleNumber ?? ""}
              onChange={(v) => setField("cycleNumber", v)}
              error={fieldErrors.cycleNumber}
            />
          </Box>
        </Box>

        <Divider />

        {/* Section 3: Quality & Expiry */}
        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[2] = el; }}
          data-bench-section="2"
          sx={sectionWrapperSx}
        >
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
            3. QUALITY SPECIFICATIONS & EXPIRY
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
              <NumericEntryField
                label="pH (at 25°C) *"
                kind="decimal"
                value={form.ph ?? ""}
                onChange={(v) => setField("ph", v)}
                error={fieldErrors.ph}
                aria-describedby={hintDescribedBy("media-ph")}
              />
              <FieldHint id="media-ph" label="the lot's pH" hint={hints.ph} />
            </Stack>

            <DateField
              label="Expiry Date *"
              value={form.expiryDate ?? ""}
              onChange={(v) => setField("expiryDate", v)}
              error={Boolean(fieldErrors.expiryDate)}
              helperText={fieldErrors.expiryDate}
            />
          </Box>
        </Box>

        <Divider />

        <Box
          ref={(el: HTMLDivElement | null) => { sectionRefs.current[3] = el; }}
          data-bench-section="3"
          sx={sectionWrapperSx}
        >
          <MediaAdditivesSection
            rows={additives}
            onChange={setAdditives}
            stockLots={additiveStock}
            solutions={solutions}
            nextKey={() => ++additiveKey.current}
          />
        </Box>
      </Stack>
    </>
  );

  if (benchMode) {
    return (
      <BenchDialog
        open={open}
        title={dialogTitle}
        onClose={onClose}
        onSave={() => void submitPreparation()}
        saving={saving}
        saveLabel="Save Prepared Lot"
        dirty={isDirty}
        maxWidth="md"
      >
        {formBody}
      </BenchDialog>
    );
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <form onSubmit={handleSubmit}>
        <DialogTitle sx={{ pb: 1 }}>{dialogTitle}</DialogTitle>
        <DialogContent dividers>{formBody}</DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={onClose} variant="outlined" disabled={saving}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={saving}
            color="primary"
            sx={{ fontWeight: 700 }}
          >
            {saving ? "Saving…" : "Save Prepared Lot"}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
