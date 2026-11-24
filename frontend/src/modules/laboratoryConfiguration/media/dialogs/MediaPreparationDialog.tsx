import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "../../../../i18n";
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
import { LastChoiceField, useLastChoiceDefault } from "../../../../components/entry/LastChoiceField";
import { entryDefaultsService } from "../../../../services/entryDefaultsService";
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
// The server stamps the lot's preparation date from its UTC clock and checks
// the expiry against that date, so compare against the UTC date here too.
function utcTodayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

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


interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (newLot: MediaLot) => void;
}

export function MediaPreparationDialog({ open, onClose, onSuccess }: Props) {
  const { t } = useTranslation("labConfig");
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
  const [expiryIncomplete, setExpiryIncomplete] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<MediaFieldKey, string>>>({});
  const [saving, setSaving] = useState(false);
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [activeSection, setActiveSection] = useState(0);
  const sectionRefs = useRef<(HTMLDivElement | null)[]>([]);
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();
  const autoclaveLastChoice = useLastChoiceDefault<number>("equipment");

  const { applyDefault: applyAutoclaveDefault, reset: resetAutoclaveDefault } = autoclaveLastChoice;
  const [offeredAutoclaveId, setOfferedAutoclaveId] = useState<number | null>(null);
  const defaultAppliedRef = useRef(false);
  const formRef = useRef(form);
  formRef.current = form;

  useEffect(() => {
    if (open) {
      setError(null);
      setFieldErrors({});
      setKeptOffline(false);
      setActiveSection(0);
      setForm({});
      setAdditives([]);
      setAutoclavePrograms([]);
      setOfferedAutoclaveId(null);
      defaultAppliedRef.current = false;
      resetAutoclaveDefault();
      resetFailures();
      masterDataOptions.getEquipment("Autoclave").then(setAutoclaves).catch(fail("autoclaves", () => setAutoclaves([])));
      MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia).catch(fail("dehydrated media stock", () => setDehydratedMedia([])));
      MaterialService.getAll().then(setAllStock).catch(fail("additive stock lots", () => setAllStock([])));
    }
  }, [open, fail, resetFailures, setKeptOffline, resetAutoclaveDefault]);

  // The server offers the user's last autoclave only if it would pass the save-time checks.
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    entryDefaultsService
      .get({ context: "mediaPreparation" }, controller.signal)
      .then((res) => {
        if (controller.signal.aborted) return;
        setOfferedAutoclaveId(res.defaults.find((d) => d.field === "autoclaveEquipmentId")?.id ?? null);
      })
      .catch(() => {
        // A missing default is not an error: the field simply stays empty.
      });
    return () => controller.abort();
  }, [open]);

  useEffect(() => {
    if (offeredAutoclaveId === null) return;
    if (!autoclaves.some((a) => a.id === offeredAutoclaveId)) return;
    if (defaultAppliedRef.current || formRef.current.autoclaveEquipmentId) return;
    defaultAppliedRef.current = true;
    const controller = new AbortController();
    setForm((f) => ({ ...f, autoclaveEquipmentId: offeredAutoclaveId }));
    applyAutoclaveDefault(offeredAutoclaveId);
    EquipmentConfigurationService.getAutoclavePrograms(offeredAutoclaveId, true)
      .then((programs) => {
        if (!controller.signal.aborted) setAutoclavePrograms(Array.isArray(programs) ? programs : []);
      })
      .catch(() => {
        if (!controller.signal.aborted) setAutoclavePrograms([]);
      });
    return () => controller.abort();
  }, [offeredAutoclaveId, autoclaves, applyAutoclaveDefault]);

  const usableStock = dehydratedMedia.filter((m) => m.status === "InStock");
  const selectedMaterial = usableStock.find((m) => m.id === form.materialId);

  const sectionId: number | undefined = selectedMaterial?.sectionId;

  const benchSections = [
    t("media.preparationDialog.benchSections.preparationDetails"),
    t("media.preparationDialog.benchSections.sterilization"),
    t("media.preparationDialog.benchSections.quality"),
    t("media.preparationDialog.benchSections.additives")
  ];

  const draftValues = useMemo<MediaDraftValues>(() => ({ ...form, additives }), [form, additives]);
  const draft = useBenchDraft<MediaDraftValues>({
    formKey: "media-preparation",
    recordKey: "new",
    recordVersion: null,
    title: selectedMaterial
      ? t("media.preparationDialog.draftTitleNamed", { name: selectedMaterial.materialName })
      : t("media.preparationDialog.draftTitle"),
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
    autoclaveLastChoice.notifyValueChanged();
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
      setError(t("media.preparationDialog.errors.materialRequired"));
      return;
    }
    if (selectedMaterial && selectedMaterial.mediaProductId == null) {
      setError(t("media.preparationDialog.errors.notLinked"));
      return;
    }
    if (!form.totalWeight) {
      setError(t("media.preparationDialog.errors.weightRequired"));
      return;
    }
    if (!form.totalVolume) {
      setError(t("media.preparationDialog.errors.volumeRequired"));
      return;
    }
    if (!form.autoclaveEquipmentId) {
      setError(t("media.preparationDialog.errors.autoclaveRequired"));
      return;
    }
    if (!form.autoclaveProgram) {
      setError(t("media.preparationDialog.errors.programRequired"));
      return;
    }
    if (!form.loadType) {
      setError(t("media.preparationDialog.errors.loadTypeRequired"));
      return;
    }
    if (!form.temperature) {
      setError(t("media.preparationDialog.errors.temperatureRequired"));
      return;
    }
    if (!form.cycleTime) {
      setError(t("media.preparationDialog.errors.cycleTimeRequired"));
      return;
    }
    if (!form.cycleNumber) {
      setError(t("media.preparationDialog.errors.cycleNumberRequired"));
      return;
    }
    if (!form.ph) {
      setError(t("media.preparationDialog.errors.phRequired"));
      return;
    }
    if (expiryIncomplete) {
      setError(t("media.preparationDialog.errors.expiryIncomplete"));
      return;
    }
    if (!form.expiryDate) {
      setError(t("media.preparationDialog.errors.expiryRequired"));
      return;
    }
    // The lot is prepared today, so an expiry before today is already expired.
    if (form.expiryDate < utcTodayIso()) {
      setError(t("media.preparationDialog.errors.expiryInPast"));
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
      setError(getErrorMessage(err, t("media.preparationDialog.errors.prepareFailed")));
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
          {t("media.preparationDialog.title")}
        </Typography>
      </Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
        {t("media.preparationDialog.subtitle")}
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
          aria-label={t("media.preparationDialog.stepsAriaLabel")}
          sx={{ mb: 2 }}
          data-testid="media-step-indicator"
        >
          {benchSections.map((label, index) => (
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
            {t("media.preparationDialog.section1Heading")}
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, alignItems: "start" }}>
            <Stack spacing={1}>
              <ScanField
                label={t("media.preparationDialog.scanDehydratedLabel")}
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
                  inputProps={{ "aria-label": t("media.preparationDialog.stockAriaLabel") }}
                >
                  <MenuItem value="">
                    <em>{t("media.preparationDialog.stockPlaceholder")}</em>
                  </MenuItem>
                  {usableStock.map((m) => {
                    const codePrefix = m.code ? `${m.code}: ` : "";
                    const label = m.mediaProductId == null
                      ? t("media.preparationDialog.stockOptionUnlinked", { prefix: codePrefix, name: m.materialName, batch: m.batchNumber })
                      : t("media.preparationDialog.stockOptionLinked", {
                          prefix: codePrefix,
                          name: m.materialName,
                          batch: m.batchNumber,
                          remaining: m.quantityRemaining,
                          unit: m.unit
                        });

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
                  {t("media.preparationDialog.manufacturerBatch", {
                    manufacturer: selectedMaterial.manufacturerName,
                    batch: selectedMaterial.batchNumber
                  })}
                </Typography>
              )}
            </Stack>

            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
              <NumericEntryField
                label={t("media.preparationDialog.totalWeightLabel")}
                kind="decimal"
                value={form.totalWeight ?? ""}
                onChange={(v) => setField("totalWeight", v)}
                unit="g"
                error={fieldErrors.totalWeight}
                aria-describedby={hintDescribedBy("media-total-weight")}
              />
              <FieldHint id="media-total-weight" label={t("media.preparationDialog.totalWeightHintLabel")} hint={hints.totalWeight} />
            </Stack>

            <TextField
              size="small"
              label={t("media.preparationDialog.totalVolumeLabel")}
              placeholder={t("media.preparationDialog.totalVolumePlaceholder")}
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
            {t("media.preparationDialog.section2Heading")}
          </Typography>
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              mb: 1.5
            }}>
            {t("media.preparationDialog.section2Desc")}
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, alignItems: "start" }}>
            <Stack spacing={1}>
              <ScanField
                label={t("media.preparationDialog.scanAutoclaveLabel")}
                expectedType="EquipmentAsset"
                offeredIds={autoclaves.map((a) => a.id)}
                onSelect={(id) => {
                  void handleAutoclaveChange(id);
                }}
                disabled={saving}
              />
              <LastChoiceField kind="equipment" isDefault={autoclaveLastChoice.isDefault}>
                <FormControl fullWidth size="small" error={Boolean(fieldErrors.autoclaveEquipmentId)}>
                  <Select
                    size="small"
                    displayEmpty
                    fullWidth
                    value={form.autoclaveEquipmentId ?? ""}
                    onChange={(e) => {
                      void handleAutoclaveChange(e.target.value as number | "");
                    }}
                    inputProps={{ "aria-label": t("media.preparationDialog.autoclaveAriaLabel") }}
                  >
                    <MenuItem value="">
                      <em>{t("media.preparationDialog.selectAutoclave")}</em>
                    </MenuItem>
                    {autoclaves.map((a) => (
                      <MenuItem key={a.id} value={a.id}>
                        {a.code}: {a.name}
                      </MenuItem>
                    ))}
                  </Select>
                  {fieldErrors.autoclaveEquipmentId && <FormHelperText>{fieldErrors.autoclaveEquipmentId}</FormHelperText>}
                </FormControl>
              </LastChoiceField>
            </Stack>

            <FormControl fullWidth size="small" error={Boolean(fieldErrors.autoclaveProgram)}>
              <Select
                size="small"
                displayEmpty
                disabled={!form.autoclaveEquipmentId || !Array.isArray(autoclavePrograms) || autoclavePrograms.length === 0}
                value={form.autoclaveProgramId ?? ""}
                onChange={(e) => handleProgramChange(e.target.value as number | "")}
                inputProps={{ "aria-label": t("media.preparationDialog.programAriaLabel") }}
              >
                <MenuItem value="">
                  <em>{!Array.isArray(autoclavePrograms) || autoclavePrograms.length === 0 ? t("media.preparationDialog.noActivePrograms") : t("media.preparationDialog.selectProgram")}</em>
                </MenuItem>
                {Array.isArray(autoclavePrograms) && autoclavePrograms.map((p) => (
                  <MenuItem key={p.id} value={p.id}>
                    {t("media.preparationDialog.programOption", {
                      code: p.programCode,
                      name: p.programName,
                      temp: p.temperature,
                      minutes: p.cycleTimeMinutes
                    })}
                  </MenuItem>
                ))}
              </Select>
              {fieldErrors.autoclaveProgram && <FormHelperText>{fieldErrors.autoclaveProgram}</FormHelperText>}
            </FormControl>

            <TextField
              size="small"
              label={t("media.preparationDialog.loadTypeLabel")}
              value={form.loadType ?? ""}
              helperText={t("media.preparationDialog.configuredInLabConfig")}
              slotProps={{
                input: { readOnly: true }
              }}
            />

            <NumericEntryField
              label={t("media.preparationDialog.temperatureLabel")}
              kind="decimal"
              value={form.temperature === "" || form.temperature == null ? "" : String(form.temperature)}
              onChange={() => {}}
              readOnly
              unit="°C"
              helperText={t("media.preparationDialog.configuredInLabConfig")}
            />

            <NumericEntryField
              label={t("media.preparationDialog.cycleTimeLabel")}
              kind="decimal"
              value={form.cycleTime === "" || form.cycleTime == null ? "" : String(form.cycleTime)}
              onChange={() => {}}
              readOnly
              unit="min"
              helperText={t("media.preparationDialog.configuredInLabConfig")}
            />

            <NumericEntryField
              label={t("media.preparationDialog.cycleNumberLabel")}
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
            {t("media.preparationDialog.section3Heading")}
          </Typography>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
              <NumericEntryField
                label={t("media.preparationDialog.phLabel")}
                kind="decimal"
                value={form.ph ?? ""}
                onChange={(v) => setField("ph", v)}
                error={fieldErrors.ph}
                aria-describedby={hintDescribedBy("media-ph")}
              />
              <FieldHint id="media-ph" label={t("media.preparationDialog.phHintLabel")} hint={hints.ph} />
            </Stack>

            <DateField
              label={t("media.preparationDialog.expiryDateLabel")}
              value={form.expiryDate ?? ""}
              onChange={(v) => setField("expiryDate", v)}
              onIncompleteChange={setExpiryIncomplete}
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

  const closeBench = async () => {
    // Leaving a form with entries was confirmed as a discard; an offered draft nobody accepted is left alone.
    if (isDirty && !draft.restored) await draft.discard();
    onClose();
  };

  if (benchMode) {
    return (
      <BenchDialog
        open={open}
        title={dialogTitle}
        onClose={() => void closeBench()}
        onSave={() => void submitPreparation()}
        saving={saving}
        saveLabel={t("media.preparationDialog.saveLabel")}
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
            {t("media.preparationDialog.cancel")}
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={saving}
            color="primary"
            sx={{ fontWeight: 700 }}
          >
            {saving ? t("media.preparationDialog.saving") : t("media.preparationDialog.saveLabel")}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
