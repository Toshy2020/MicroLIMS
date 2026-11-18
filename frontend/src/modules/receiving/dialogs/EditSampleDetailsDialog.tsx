import { useEffect, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  Typography,
  useTheme
} from "@mui/material";
import { SampleCorrectionPayload, SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { DateField } from "../../../components/DateField";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { Trans } from "react-i18next";
import { FieldHint, describedByWithHint, hintDescribedBy } from "../../../components/FieldHint";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { getReceivingHints } from "../hints";

interface Props {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onSuccess: () => void;
}

interface LookupOption {
  id: number;
  name: string;
  isActive?: boolean;
}

interface FormState {
  controlNumber: string;
  sampledBy: string;
  causeOfTestingId: number | "";
  batchNumber: string;
  mfgDate: string;
  expDate: string;
  sampleQuantity: string;
  productionStage: string;
  previousProductName: string;
  previousProductBatchNumber: string;
  storageCondition: string;
  storageTimeHours: string;
  locationId: number | "";
  grade: string;
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

// The statement the backend records for SignatureMeaning.SampleCorrected.
const MEANING_STATEMENT = "I confirm these corrections to the sample record are accurate and justified.";

// The item / location field for each category - it decides the sample's tests.
function locationFieldFor(category: string): { key: "item" | "waterDepartment" | "department" | "machine"; load: () => Promise<LookupOption[]> } | null {
  if (PRODUCT_LIKE.includes(category)) return { key: "item", load: () => masterDataOptions.getItems(category) };
  if (category === "Water") return { key: "waterDepartment", load: masterDataOptions.getWaterDepartments };
  if (category === "EnvironmentalMonitoring") return { key: "department", load: masterDataOptions.getDepartments };
  if (category === "AfterCleaning") return { key: "machine", load: masterDataOptions.getMachines };
  return null;
}

function currentLocationId(s: SampleRecord): number | null {
  if (PRODUCT_LIKE.includes(s.category)) return s.itemId ?? null;
  if (s.category === "Water") return s.waterDepartmentId;
  if (s.category === "EnvironmentalMonitoring") return s.departmentId;
  if (s.category === "AfterCleaning") return s.machineId;
  return null;
}

const toDateInput = (value?: string | null) => (value ? value.slice(0, 10) : "");
const trimmedOrNull = (value: string) => (value.trim() ? value.trim() : null);

function initialForm(s: SampleRecord): FormState {
  return {
    controlNumber: s.controlNumber ?? "",
    sampledBy: s.sampledBy ?? "",
    causeOfTestingId: s.causeOfTestingId ?? "",
    batchNumber: s.batchNumber ?? "",
    mfgDate: toDateInput(s.mfgDate),
    expDate: toDateInput(s.expDate),
    sampleQuantity: s.sampleQuantity ?? "",
    productionStage: s.productionStage ?? "",
    previousProductName: s.previousProductName ?? "",
    previousProductBatchNumber: s.previousProductBatchNumber ?? "",
    storageCondition: s.storageCondition ?? "",
    storageTimeHours: s.storageTimeHours != null ? String(s.storageTimeHours) : "",
    locationId: currentLocationId(s) ?? "",
    grade: s.grade ?? ""
  };
}

// Signed correction of a received sample's details. Every field is sent in
// full; the backend works out what changed, enforces the lock (details until
// submitted for review, item / location until testing starts), signs, and
// writes each field's old and new value to the sample's audit trail.
export function EditSampleDetailsDialog({ open, sample, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  const hints = getReceivingHints(t);
  const [form, setForm] = useState<FormState | null>(null);
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [causes, setCauses] = useState<LookupOption[]>([]);
  const [samplers, setSamplers] = useState<LookupOption[]>([]);
  const [productionStages, setProductionStages] = useState<LookupOption[]>([]);
  const [locations, setLocations] = useState<LookupOption[]>([]);
  const [grades, setGrades] = useState<string[]>([]);

  const category = sample?.category ?? "";

  useEffect(() => {
    if (open && sample) {
      setForm(initialForm(sample));
      setReason("");
      setSigning(false);
    }
  }, [sample, open]);

  useEffect(() => {
    if (!open || !category) return;
    let cancelled = false;
    const locationField = locationFieldFor(category);
    setLoadError(null);
    Promise.all([
      masterDataOptions.getCausesOfTesting(),
      masterDataOptions.getSamplers(),
      category === "FinishedProduct" ? masterDataOptions.getProductionStages() : Promise.resolve([]),
      locationField ? locationField.load() : Promise.resolve([])
    ])
      .then(([causeList, samplerList, stageList, locationList]) => {
        if (cancelled) return;
        setCauses(causeList);
        setSamplers(samplerList);
        setProductionStages(stageList);
        setLocations(locationList);
      })
      .catch(() => {
        if (!cancelled) setLoadError(t("editSample.loadFailed"));
      });
    return () => {
      cancelled = true;
    };
  }, [open, category, t]);

  // The grades of the item the sample will have: the chosen one when the item is being changed.
  const gradeItemId = PRODUCT_LIKE.includes(category)
    ? (form?.locationId === "" ? null : form?.locationId) ?? sample?.itemId ?? null
    : null;
  useEffect(() => {
    if (!open || gradeItemId == null) {
      setGrades([]);
      return;
    }
    let cancelled = false;
    ReceiveService.itemGrades(gradeItemId)
      .then((list) => !cancelled && setGrades(list))
      .catch(() => !cancelled && setGrades([]));
    return () => {
      cancelled = true;
    };
  }, [open, gradeItemId]);

  if (!sample || !form) return null;

  const isProductLike = PRODUCT_LIKE.includes(category);
  const isWater = category === "Water";
  const isAfterCleaning = category === "AfterCleaning";
  const locationField = locationFieldFor(category);
  const originalLocationId = currentLocationId(sample);
  const locationChanged = form.locationId !== "" && form.locationId !== originalLocationId;
  const canEdit = Boolean(sample.canEditDetails);
  const canChangeLocation = Boolean(sample.canChangeItemOrLocation);
  // Grade shows only for an item that has grades; the backend re-checks that every test is still pending.
  const showGrade = isProductLike && grades.length > 0;
  // A new item needs its own grade; otherwise only while every test is still pending.
  const canChangeGrade = canEdit && (locationChanged || sample.assignedTests.every((t) => t.status === "Pending"));
  // "Retest" is assigned by the system to retest samples - never chosen here.
  const isRetest = (name?: string) => (name ?? "").trim().toLowerCase() === "retest";
  const causeIsRetest = isRetest(sample.causeOfTesting);
  // Storage is captured at preparation; a department change discards it.
  const hasStorage = isWater && Boolean(sample.storageCondition) && !locationChanged;

  const locationOptions = locations.filter((o) => o.isActive !== false || o.id === originalLocationId);
  const causeOptions = causes.filter((c) => !isRetest(c.name) || c.id === sample.causeOfTestingId);
  const locationValue = locationOptions.some((o) => o.id === form.locationId) ? form.locationId : "";
  const causeValue = causeOptions.some((c) => c.id === form.causeOfTestingId) ? form.causeOfTestingId : "";
  const stageValue = productionStages.some((s) => s.name === form.productionStage) ? form.productionStage : "";

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));

  const missingRequired =
    !form.controlNumber.trim() ||
    !form.sampledBy.trim() ||
    form.causeOfTestingId === "" ||
    (isProductLike && !form.batchNumber.trim()) ||
    (showGrade && !form.grade) ||
    (category === "FinishedProduct" && !form.productionStage.trim()) ||
    (isAfterCleaning && (!form.previousProductName.trim() || !form.previousProductBatchNumber.trim())) ||
    (hasStorage && form.storageCondition === "Refrigerator" && form.storageTimeHours === "");
  const canSave = canEdit && !missingRequired && reason.trim().length > 0;
  // Says why Sign & Save is disabled - a greyed-out signature button with no
  // explanation leaves the analyst guessing. A locked sample is already
  // explained by the alert at the top of the form.
  const signHint = !canEdit
    ? null
    : missingRequired
      ? t("editSample.completeRequired")
      : !reason.trim()
        ? t("editSample.enterReason")
        : null;

  // A failure (wrong password, locked record, validation) is thrown back to
  // the signature dialog, which shows the server's message and stays open.
  const handleSign = async (password: string) => {
    const locationId = form.locationId === "" ? null : form.locationId;
    const payload: SampleCorrectionPayload = {
      reason: reason.trim(),
      password,
      controlNumber: form.controlNumber.trim(),
      sampledBy: form.sampledBy.trim(),
      causeOfTestingId: Number(form.causeOfTestingId),
      batchNumber: trimmedOrNull(form.batchNumber),
      mfgDate: form.mfgDate || null,
      expDate: form.expDate || null,
      sampleQuantity: trimmedOrNull(form.sampleQuantity),
      productionStage: trimmedOrNull(form.productionStage),
      previousProductName: trimmedOrNull(form.previousProductName),
      previousProductBatchNumber: trimmedOrNull(form.previousProductBatchNumber),
      storageCondition: form.storageCondition || null,
      storageTimeHours:
        form.storageCondition === "Refrigerator" && form.storageTimeHours !== "" ? Number(form.storageTimeHours) : null,
      itemId: isProductLike ? locationId : null,
      waterDepartmentId: isWater ? locationId : null,
      departmentId: category === "EnvironmentalMonitoring" ? locationId : null,
      machineId: isAfterCleaning ? locationId : null,
      grade: showGrade ? form.grade || null : null
    };
    await ReceiveService.correctSample(sample.sampleId, payload, sample.version);
    setSigning(false);
    onSuccess();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open}
        onClose={onClose}
        maxWidth="md"
        title={
          <Box>
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main }}>
              {t("editSample.title")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="editSample.subtitle"
                ns="receiving"
                values={{ sampleId: sample.sampleId, displayName: sample.displayName, referenceNumber: sample.referenceNumber }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>
        }
        actions={
          <>
            {signHint && (
              <Typography role="status" sx={{ fontSize: 12, color: "text.secondary", me: "auto", ps: 1 }}>
                {signHint}
              </Typography>
            )}
            <Button onClick={onClose} color="inherit">
              {t("editSample.cancel")}
            </Button>
            <Button
              variant="contained"
              color="primary"
              onClick={() => setSigning(true)}
              disabled={!canSave}
              sx={{ fontWeight: 600 }}
            >
              {t("editSample.signAndSave")}
            </Button>
          </>
        }
      >
        {!canEdit && (
          <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
            {t("editSample.lockedForReview")}
          </Alert>
        )}
        {loadError && (
          <Alert severity="error" sx={{ mb: 2, fontSize: 13 }}>
            {loadError}
          </Alert>
        )}

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, pt: 1 }}>
          {locationField && (!canEdit || !canChangeLocation) && (
            // Locked: shown as a read-only value with a lock, not a disabled
            // dropdown whose chevron still suggests it can be opened.
            <TextField
              label={t(`editSample.locationFields.${locationField.key}`)}
              size="small"
              fullWidth
              value={locationOptions.find((o) => o.id === form.locationId)?.name ?? ""}
              helperText={canChangeLocation ? undefined : t("editSample.lockedTestingStarted")}
              sx={{ gridColumn: "1 / -1" }}
              slotProps={{
                input: {
                  readOnly: true,
                  endAdornment: (
                    <InputAdornment position="end">
                      <LockOutlinedIcon fontSize="small" sx={{ color: "text.disabled" }} titleAccess={t("editSample.lockedAccess")} />
                    </InputAdornment>
                  )
                },
                inputLabel: { shrink: true }
              }}
            />
          )}
          {locationField && canEdit && canChangeLocation && (
            <TextField
              select
              label={t(`editSample.locationFields.${locationField.key}`)}
              size="small"
              fullWidth
              value={locationValue}
              onChange={(e) => {
                const id = Number(e.target.value);
                setForm((prev) => (prev ? { ...prev, locationId: id, grade: id === originalLocationId ? (sample.grade ?? "") : "" } : prev));
              }}
              sx={{ gridColumn: "1 / -1" }}
            >
              {locationOptions.map((o) => (
                <MenuItem key={o.id} value={o.id}>
                  {o.name}
                </MenuItem>
              ))}
            </TextField>
          )}

          {locationChanged && locationField && (
            <Alert severity="warning" sx={{ gridColumn: "1 / -1", fontSize: 12.5 }}>
              {t("editSample.locationChangedWarning", { location: t(`editSample.locationFieldsLower.${locationField.key}`) })}
            </Alert>
          )}

          {isProductLike && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
              <TextField
                slotProps={{ input: { "aria-describedby": hintDescribedBy("receiving-batch-number") } }}
                label={t("editSample.batchNumber")}
                required
                size="small"
                fullWidth
                value={form.batchNumber}
                onChange={(e) => set("batchNumber", e.target.value)}
                disabled={!canEdit}
              />
              <FieldHint id="receiving-batch-number" label={t("editSample.batchNumber")} hint={hints.batchNumber} />
            </Stack>
          )}

          {showGrade && (
            <TextField
              select
              label={t("editSample.grade")}
              required
              size="small"
              fullWidth
              value={grades.includes(form.grade) ? form.grade : ""}
              onChange={(e) => set("grade", e.target.value)}
              disabled={!canChangeGrade}
              helperText={
                canChangeGrade
                  ? t("editSample.gradeHelper")
                  : canEdit
                    ? t("editSample.lockedTestingStarted")
                    : undefined
              }
            >
              {grades.map((g) => (
                <MenuItem key={g} value={g}>
                  {g}
                </MenuItem>
              ))}
            </TextField>
          )}

          <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
            <TextField
              slotProps={{ input: { "aria-describedby": hintDescribedBy("receiving-control-number") } }}
              label={t("editSample.controlNumber")}
              required
              size="small"
              fullWidth
              value={form.controlNumber}
              onChange={(e) => set("controlNumber", e.target.value)}
              disabled={!canEdit}
            />
            <FieldHint id="receiving-control-number" label={t("editSample.controlNumber")} hint={hints.controlNumber} />
          </Stack>

          <TextField
            select
            label={t("editSample.causeOfTesting")}
            required
            size="small"
            fullWidth
            value={causeValue}
            onChange={(e) => set("causeOfTestingId", Number(e.target.value))}
            disabled={!canEdit || causeIsRetest}
            helperText={causeIsRetest ? t("editSample.causeRetestHelper") : undefined}
          >
            {causeOptions.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </TextField>

          <Autocomplete
            freeSolo
            options={samplers.map((s) => s.name)}
            inputValue={form.sampledBy}
            onInputChange={(_, value) => set("sampledBy", value)}
            disabled={!canEdit}
            renderInput={(params) => <TextField {...params} label={t("editSample.sampledBy")} required size="small" />}
          />

          {(isProductLike || isWater) && (
            <TextField
              label={t("editSample.sampleQuantity")}
              size="small"
              fullWidth
              value={form.sampleQuantity}
              onChange={(e) => set("sampleQuantity", e.target.value)}
              disabled={!canEdit}
            />
          )}

          {category === "FinishedProduct" && (
            <TextField
              select
              label={t("editSample.productionStage")}
              required
              size="small"
              fullWidth
              value={stageValue}
              onChange={(e) => set("productionStage", e.target.value)}
              disabled={!canEdit}
            >
              {productionStages.map((s) => (
                <MenuItem key={s.id} value={s.name}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>
          )}

          {isProductLike && (
            <>
              <DateField
                label={t("editSample.mfgDate")}
                fullWidth
                value={form.mfgDate}
                onChange={(v) => set("mfgDate", v)}
                disabled={!canEdit}
              />
              <DateField
                label={t("editSample.expDate")}
                fullWidth
                value={form.expDate}
                onChange={(v) => set("expDate", v)}
                disabled={!canEdit}
              />
            </>
          )}

          {isAfterCleaning && (
            <>
              <TextField
                label={t("editSample.previousProduct")}
                required
                size="small"
                fullWidth
                value={form.previousProductName}
                onChange={(e) => set("previousProductName", e.target.value)}
                disabled={!canEdit}
              />
              <TextField
                label={t("editSample.previousProductBatchNumber")}
                required
                size="small"
                fullWidth
                value={form.previousProductBatchNumber}
                onChange={(e) => set("previousProductBatchNumber", e.target.value)}
                disabled={!canEdit}
              />
            </>
          )}

          {hasStorage && (
            <>
              <TextField
                select
                label={t("editSample.storageCondition")}
                required
                size="small"
                fullWidth
                value={form.storageCondition}
                onChange={(e) => set("storageCondition", e.target.value)}
                disabled={!canEdit}
              >
                <MenuItem value="RoomTemperature">{t("editSample.roomTemperature")}</MenuItem>
                <MenuItem value="Refrigerator">{t("editSample.refrigerator")}</MenuItem>
              </TextField>
              {form.storageCondition === "Refrigerator" && (
                <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
                  <TextField
                    label={t("editSample.storageTimeHours")}
                    type="number"
                    required
                    size="small"
                    fullWidth
                    value={form.storageTimeHours}
                    onChange={(e) => set("storageTimeHours", e.target.value)}
                    disabled={!canEdit}
                    slotProps={{
                      htmlInput: { min: 0 },
                      input: { "aria-describedby": hintDescribedBy("receiving-storage-time") }
                    }}
                  />
                  <FieldHint id="receiving-storage-time" label={t("editSample.storageTimeHours")} hint={hints.storageTime} />
                </Stack>
              )}
            </>
          )}

          <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start", minWidth: 0 }}>
            <TextField
              id="receiving-reason-for-correction"
              slotProps={{
                input: {
                  "aria-describedby": describedByWithHint("receiving-reason-for-correction", "receiving-reason-for-correction")
                }
              }}
              label={t("editSample.reasonForCorrection")}
              required
              multiline
              rows={2}
              fullWidth
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={!canEdit}
              helperText={t("editSample.reasonHelper")}
              sx={{ gridColumn: "1 / -1" }}
            />
            <FieldHint id="receiving-reason-for-correction" label={t("editSample.reasonForCorrection")} hint={hints.reasonForCorrection} />
          </Stack>
        </Box>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={MEANING_STATEMENT}
        onCancel={() => setSigning(false)}
        onConfirm={handleSign}
      />
    </>
  );
}
