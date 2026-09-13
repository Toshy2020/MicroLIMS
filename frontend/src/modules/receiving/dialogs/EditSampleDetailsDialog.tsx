import { useEffect, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  MenuItem,
  TextField,
  Typography,
  useTheme
} from "@mui/material";
import { SampleCorrectionPayload, SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";

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
}

const PRODUCT_LIKE = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

// The statement the backend records for SignatureMeaning.SampleCorrected.
const MEANING_STATEMENT = "I confirm these corrections to the sample record are accurate and justified.";

// The item / location field for each category - it decides the sample's tests.
function locationFieldFor(category: string): { label: string; load: () => Promise<LookupOption[]> } | null {
  if (PRODUCT_LIKE.includes(category)) return { label: "Item", load: () => masterDataOptions.getItems(category) };
  if (category === "Water") return { label: "Water Department", load: masterDataOptions.getWaterDepartments };
  if (category === "EnvironmentalMonitoring") return { label: "Department", load: masterDataOptions.getDepartments };
  if (category === "AfterCleaning") return { label: "Machine", load: masterDataOptions.getMachines };
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
    locationId: currentLocationId(s) ?? ""
  };
}

// Signed correction of a received sample's details. Every field is sent in
// full; the backend works out what changed, enforces the lock (details until
// submitted for review, item / location until testing starts), signs, and
// writes each field's old and new value to the sample's audit trail.
export function EditSampleDetailsDialog({ open, sample, onClose, onSuccess }: Props) {
  const theme = useTheme();
  const [form, setForm] = useState<FormState | null>(null);
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [causes, setCauses] = useState<LookupOption[]>([]);
  const [samplers, setSamplers] = useState<LookupOption[]>([]);
  const [productionStages, setProductionStages] = useState<LookupOption[]>([]);
  const [locations, setLocations] = useState<LookupOption[]>([]);

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
        if (!cancelled) setLoadError("Failed to load the lookup lists. Close the dialog and try again.");
      });
    return () => {
      cancelled = true;
    };
  }, [open, category]);

  if (!sample || !form) return null;

  const isProductLike = PRODUCT_LIKE.includes(category);
  const isWater = category === "Water";
  const isAfterCleaning = category === "AfterCleaning";
  const locationField = locationFieldFor(category);
  const originalLocationId = currentLocationId(sample);
  const locationChanged = form.locationId !== "" && form.locationId !== originalLocationId;
  const canEdit = Boolean(sample.canEditDetails);
  const canChangeLocation = Boolean(sample.canChangeItemOrLocation);
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
    (isAfterCleaning && (!form.previousProductName.trim() || !form.previousProductBatchNumber.trim())) ||
    (hasStorage && form.storageCondition === "Refrigerator" && form.storageTimeHours === "");
  const canSave = canEdit && !missingRequired && reason.trim().length > 0;

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
      machineId: isAfterCleaning ? locationId : null
    };
    await ReceiveService.correctSample(sample.sampleId, payload);
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
              Edit Sample Details
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Sample #{sample.sampleId} — {sample.displayName} ({sample.referenceNumber})
            </Typography>
          </Box>
        }
        actions={
          <>
            <Button onClick={onClose} color="inherit">
              Cancel
            </Button>
            <Button
              variant="contained"
              color="primary"
              onClick={() => setSigning(true)}
              disabled={!canSave}
              sx={{ fontWeight: 600 }}
            >
              Sign &amp; Save
            </Button>
          </>
        }
      >
        {!canEdit && (
          <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
            This sample has been submitted for review, so its details are locked.
          </Alert>
        )}
        {loadError && (
          <Alert severity="error" sx={{ mb: 2, fontSize: 13 }}>
            {loadError}
          </Alert>
        )}

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, pt: 1 }}>
          {locationField && (
            <TextField
              select
              label={locationField.label}
              size="small"
              fullWidth
              value={locationValue}
              onChange={(e) => set("locationId", Number(e.target.value))}
              disabled={!canEdit || !canChangeLocation}
              helperText={canChangeLocation ? undefined : "Locked: testing has started for this sample"}
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
              Changing the {locationField.label.toLowerCase()} rebuilds the sample's tests and resets its
              preparation - the sample has to be prepared again.
            </Alert>
          )}

          {isProductLike && (
            <TextField
              label="Batch Number"
              required
              size="small"
              fullWidth
              value={form.batchNumber}
              onChange={(e) => set("batchNumber", e.target.value)}
              disabled={!canEdit}
            />
          )}

          <TextField
            label="Control Number"
            required
            size="small"
            fullWidth
            value={form.controlNumber}
            onChange={(e) => set("controlNumber", e.target.value)}
            disabled={!canEdit}
          />

          <TextField
            select
            label="Cause of Testing"
            required
            size="small"
            fullWidth
            value={causeValue}
            onChange={(e) => set("causeOfTestingId", Number(e.target.value))}
            disabled={!canEdit || causeIsRetest}
            helperText={causeIsRetest ? "Assigned by the system to retest samples" : undefined}
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
            renderInput={(params) => <TextField {...params} label="Sampled By" required size="small" />}
          />

          {(isProductLike || isWater) && (
            <TextField
              label="Sample Quantity"
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
              label="Production Stage"
              size="small"
              fullWidth
              value={stageValue}
              onChange={(e) => set("productionStage", e.target.value)}
              disabled={!canEdit}
            >
              <MenuItem value="">
                <em>None</em>
              </MenuItem>
              {productionStages.map((s) => (
                <MenuItem key={s.id} value={s.name}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>
          )}

          {isProductLike && (
            <>
              <TextField
                label="Mfg Date"
                type="date"
                size="small"
                fullWidth
                value={form.mfgDate}
                onChange={(e) => set("mfgDate", e.target.value)}
                disabled={!canEdit}
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                label="Exp Date"
                type="date"
                size="small"
                fullWidth
                value={form.expDate}
                onChange={(e) => set("expDate", e.target.value)}
                disabled={!canEdit}
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </>
          )}

          {isAfterCleaning && (
            <>
              <TextField
                label="Previous Product"
                required
                size="small"
                fullWidth
                value={form.previousProductName}
                onChange={(e) => set("previousProductName", e.target.value)}
                disabled={!canEdit}
              />
              <TextField
                label="Previous Product Batch Number"
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
                label="Storage Condition"
                required
                size="small"
                fullWidth
                value={form.storageCondition}
                onChange={(e) => set("storageCondition", e.target.value)}
                disabled={!canEdit}
              >
                <MenuItem value="RoomTemperature">Room Temperature</MenuItem>
                <MenuItem value="Refrigerator">Refrigerator</MenuItem>
              </TextField>
              {form.storageCondition === "Refrigerator" && (
                <TextField
                  label="Storage Time (hours)"
                  type="number"
                  required
                  size="small"
                  fullWidth
                  value={form.storageTimeHours}
                  onChange={(e) => set("storageTimeHours", e.target.value)}
                  disabled={!canEdit}
                  slotProps={{ htmlInput: { min: 0 } }}
                />
              )}
            </>
          )}

          <TextField
            label="Reason for Correction"
            required
            multiline
            rows={2}
            fullWidth
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={!canEdit}
            helperText="Recorded with your electronic signature and in the sample's audit trail."
            sx={{ gridColumn: "1 / -1" }}
          />
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
