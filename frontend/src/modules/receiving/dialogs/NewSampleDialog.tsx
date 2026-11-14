import { useCallback, useEffect, useRef, useState } from "react";
import {
  Button,
  Box,
  Typography,
  Alert,
  Snackbar,
  CircularProgress,
  useTheme
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CheckIcon from "@mui/icons-material/Check";
import {
  BatchReceivedSample,
  BatchReceiveRequest,
  CreatedSampleRef,
  ReceiveRowItem,
  ReceivingMasterData,
  SampleCategoryKey
} from "../types/receivingTypes";
import { RECEIVING_CATEGORIES } from "../constants/receivingConstants";
import { SampleTypeSelector } from "./SampleTypeSelector";
import { FocusErrorTarget, LabMode, MAX_RECEIVE_ROWS, MultiSampleEntryGrid } from "./MultiSampleEntryGrid";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { entryDefaultsService } from "../../../services/entryDefaultsService";
import { useLastChoiceDefault } from "../../../components/entry/LastChoiceField";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { errorsByField, errorsByRow } from "../../../utils/fieldErrors";
import { getErrorMessage } from "../../../utils/errorMessage";

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (created: CreatedSampleRef[]) => void;
  // Which sample categories this dialog instance offers - the main
  // Receiving page limits this to FP/RM/PM; a lab workspace offers its
  // own set including Water/EM/After Cleaning (see RECEIVING_CATEGORIES).
  allowedCategories: SampleCategoryKey[];
  // "choose": the receiver picks target labs per row (Laboratories column).
  // "fixed": every row targets this one lab silently, no column shown.
  labMode: LabMode;
}

// The sampling time is optional; the server checks it is not after the receipt.
const sampledAtIso = (row: ReceiveRowItem) => (row.sampledAt ? new Date(row.sampledAt).toISOString() : null);

function formatHm(date: Date): string {
  const h = String(date.getHours()).padStart(2, "0");
  const m = String(date.getMinutes()).padStart(2, "0");
  return `${h}:${m}`;
}

// Explicit allow-list of receiving fields stored in the device draft.
// Excludes coaFile (File objects are never persisted/restored) and errors.
function pickReceivingDraft(values: { rows: ReceiveRowItem[] }): Partial<{ rows: ReceiveRowItem[] }> {
  return {
    rows: values.rows.map((r) => ({
      id: r.id,
      itemId: r.itemId,
      previousProductItemId: r.previousProductItemId,
      previousProductName: r.previousProductName,
      previousProductBatchNumber: r.previousProductBatchNumber,
      techniques: r.techniques ? [...r.techniques] : [],
      productionStage: r.productionStage,
      waterSamplingPointId: r.waterSamplingPointId,
      departmentId: r.departmentId,
      machineId: r.machineId,
      causeOfTestingId: r.causeOfTestingId,
      sampleQuantity: r.sampleQuantity,
      sampledBy: r.sampledBy,
      sampledAt: r.sampledAt,
      batchNumber: r.batchNumber,
      controlNumber: r.controlNumber,
      mfgDate: r.mfgDate,
      expDate: r.expDate,
      targetSectionIds: r.targetSectionIds ? [...r.targetSectionIds] : undefined,
      gradeOptions: r.gradeOptions ? [...r.gradeOptions] : undefined,
      grade: r.grade
    }))
  };
}

function formatErrorSummary(problemCount: number, failingRowCount: number): string {
  const p = problemCount === 1 ? "problem" : "problems";
  const r = failingRowCount === 1 ? "row" : "rows";
  return `${problemCount} ${p} in ${failingRowCount} ${r}`;
}

const createEmptyRow = (defaultValues?: Partial<ReceiveRowItem>): ReceiveRowItem => ({
  id: Math.random().toString(36).substring(2, 9),
  itemId: defaultValues?.itemId ?? "",
  previousProductItemId: defaultValues?.previousProductItemId ?? null,
  previousProductName: defaultValues?.previousProductName ?? "",
  previousProductBatchNumber: defaultValues?.previousProductBatchNumber ?? "",
  techniques: defaultValues?.techniques ?? [],
  productionStage: defaultValues?.productionStage ?? "",
  waterSamplingPointId: defaultValues?.waterSamplingPointId ?? "",
  departmentId: defaultValues?.departmentId ?? "",
  machineId: defaultValues?.machineId ?? "",
  causeOfTestingId: defaultValues?.causeOfTestingId ?? "",
  sampleQuantity: defaultValues?.sampleQuantity ?? "",
  sampledBy: defaultValues?.sampledBy ?? "",
  sampledAt: "",
  batchNumber: defaultValues?.batchNumber ?? "",
  controlNumber: defaultValues?.controlNumber ?? "",
  mfgDate: defaultValues?.mfgDate ?? "",
  expDate: defaultValues?.expDate ?? "",
  errors: {}
});

export function NewSampleDialog({ open, onClose, onSuccess, allowedCategories, labMode }: Props) {
  const theme = useTheme();
  const [step, setStep] = useState<1 | 2>(1);
  const [category, setCategory] = useState<SampleCategoryKey | null>(null);
  const [rows, setRows] = useState<ReceiveRowItem[]>([createEmptyRow()]);

  const [masterData, setMasterData] = useState<ReceivingMasterData>({
    items: [],
    waterPoints: [],
    waterDepartments: [],
    departments: [],
    machines: [],
    causes: [],
    samplers: [],
    productionStages: []
  });

  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [warningSnackbarOpen, setWarningSnackbarOpen] = useState(false);
  const [focusErrorTarget, setFocusErrorTarget] = useState<FocusErrorTarget | null>(null);
  const focusNonceRef = useRef(0);
  const [receivedByRow, setReceivedByRow] = useState<Record<number, BatchReceivedSample> | null>(null);
  const [coaErrorByRow, setCoaErrorByRow] = useState<Record<number, string>>({});
  const [retryingCoaRow, setRetryingCoaRow] = useState<number | null>(null);
  const [draftSavedAt, setDraftSavedAt] = useState<Date | null>(null);
  const causeLastChoice = useLastChoiceDefault<number>("field");
  const sampledByLastChoice = useLastChoiceDefault<string>("field");
  const notifyCauseChanged = causeLastChoice.notifyValueChanged;
  const notifySampledByChanged = sampledByLastChoice.notifyValueChanged;

  const currentCategoryDef = RECEIVING_CATEGORIES.find((c) => c.key === category);

  const draft = useBenchDraft<{ rows: ReceiveRowItem[] }>({
    formKey: "receiving",
    recordKey: category ?? "none",
    recordVersion: null,
    title: currentCategoryDef ? `Receive ${currentCategoryDef.label}` : "Receive Sample",
    values: { rows },
    pick: pickReceivingDraft,
    enabled: open && step === 2 && category !== null && !receivedByRow
  });

  const draftBaselineRef = useRef<string | null>(null);
  useEffect(() => {
    if (!open || step !== 2 || !category) {
      draftBaselineRef.current = null;
      setDraftSavedAt(null);
      return;
    }
    const json = JSON.stringify(pickReceivingDraft({ rows }));
    if (draftBaselineRef.current === null) {
      draftBaselineRef.current = json;
      return;
    }
    if (json === draftBaselineRef.current || draft.restoredStartedAt || receivedByRow) {
      return;
    }
    const timer = setTimeout(() => {
      if (draft.storageAvailable) {
        setDraftSavedAt(new Date());
      }
    }, 400);
    return () => clearTimeout(timer);
  }, [open, step, category, rows, draft.restoredStartedAt, draft.storageAvailable, receivedByRow]);

  // Load shared master data when dialog opens
  useEffect(() => {
    if (open) {
      setStep(1);
      setCategory(null);
      setRows([createEmptyRow()]);
      setErrorMessage(null);
      setFocusErrorTarget(null);
      setReceivedByRow(null);
      setCoaErrorByRow({});
      setRetryingCoaRow(null);
      setDraftSavedAt(null);

      masterDataOptions.getCausesOfTesting().then((causes) =>
        setMasterData((prev) => ({ ...prev, causes }))
      );
      masterDataOptions.getSamplers().then((samplers) =>
        setMasterData((prev) => ({ ...prev, samplers }))
      );
      masterDataOptions.getProductionStages().then((productionStages) =>
        setMasterData((prev) => ({ ...prev, productionStages }))
      );
      masterDataOptions.getWaterSamplingPoints().then((waterPoints) =>
        setMasterData((prev) => ({ ...prev, waterPoints }))
      );
      masterDataOptions.getWaterDepartments().then((waterDepartments) =>
        setMasterData((prev) => ({ ...prev, waterDepartments }))
      );
      masterDataOptions.getDepartments().then((departments) =>
        setMasterData((prev) => ({ ...prev, departments }))
      );
      masterDataOptions.getMachines().then((machines) =>
        setMasterData((prev) => ({ ...prev, machines }))
      );
    }
  }, [open]);

  // Load items when item-based category is selected or AC is selected
  useEffect(() => {
    if (category) {
      const catDef = RECEIVING_CATEGORIES.find((c) => c.key === category);
      if (catDef?.apiCategory) {
        masterDataOptions.getItems(catDef.apiCategory).then((items) =>
          setMasterData((prev) => ({ ...prev, items }))
        );
      } else if (category === "ac") {
        masterDataOptions.getItems("FinishedProduct").then((items) =>
          setMasterData((prev) => ({ ...prev, items }))
        );
      }
    }
  }, [category]);

  const handleNextStep = () => {
    if (!category) return;
    setStep(2);
    setRows([createEmptyRow()]);
    resetCauseDefault();
    resetSampledByDefault();
    setErrorMessage(null);
  };

  const rowsRef = useRef(rows);
  rowsRef.current = rows;
  const { applyDefault: applyCauseDefault, reset: resetCauseDefault } = causeLastChoice;
  const { applyDefault: applySampledByDefault, reset: resetSampledByDefault } = sampledByLastChoice;

  // Pre-fill the first row with the server-validated last choice. The values stay editable
  // and are shown as "From your last entry"; the server decides whether anything is offered.
  useEffect(() => {
    if (!open || step !== 2 || !category) return;
    const controller = new AbortController();
    entryDefaultsService
      .get({ context: "receiving", category }, controller.signal)
      .then((res) => {
        if (controller.signal.aborted) return;
        const first = rowsRef.current[0];
        if (!first) return;
        const sampledByDef = res.defaults.find((d) => d.field === "sampledBy");
        const causeDef = res.defaults.find((d) => d.field === "causeOfTestingId");
        const sampledBy = !first.sampledBy && sampledByDef?.text ? sampledByDef.text : null;
        const causeId = !first.causeOfTestingId && causeDef?.id != null ? causeDef.id : null;
        if (sampledBy === null && causeId === null) return;
        const nextFirst = {
          ...first,
          ...(sampledBy !== null ? { sampledBy } : {}),
          ...(causeId !== null ? { causeOfTestingId: causeId } : {})
        };
        const updated = [nextFirst, ...rowsRef.current.slice(1)];
        setRows(updated);
        if (draftBaselineRef.current !== null) {
          draftBaselineRef.current = JSON.stringify(pickReceivingDraft({ rows: updated }));
        }
        if (sampledBy !== null) applySampledByDefault(sampledBy);
        if (causeId !== null) applyCauseDefault(causeId);
      })
      .catch(() => {
        // A missing default is not an error: the fields simply stay empty.
      });
    return () => controller.abort();
  }, [open, step, category, applyCauseDefault, applySampledByDefault]);

  const handlePrevStep = () => {
    setStep(1);
    resetCauseDefault();
    resetSampledByDefault();
    setErrorMessage(null);
  };

  const handleChangeRow = useCallback(<K extends keyof ReceiveRowItem>(
    index: number,
    field: K,
    value: ReceiveRowItem[K]
  ) => {
    if (index === 0 && field === "causeOfTestingId") {
      notifyCauseChanged();
    }
    if (index === 0 && field === "sampledBy") {
      notifySampledByChanged();
    }
    setRows((prev) => {
      const updated = [...prev];
      const row = { ...updated[index], [field]: value };
      if (row.errors && row.errors[field]) {
        const newErrors = { ...row.errors };
        delete newErrors[field];
        row.errors = newErrors;
      }
      updated[index] = row;
      return updated;
    });
  }, [notifyCauseChanged, notifySampledByChanged]);

  const handleAddRow = () => {
    setRows((prev) => {
      if (prev.length >= MAX_RECEIVE_ROWS) return prev;
      // Intelligently prefill common fields (sampledBy, causeOfTestingId) from last row
      const lastRow = prev[prev.length - 1];
      const newRow = createEmptyRow({
        sampledBy: lastRow?.sampledBy || "",
        causeOfTestingId: lastRow?.causeOfTestingId || "",
        techniques: lastRow?.techniques ? [...lastRow.techniques] : []
      });
      return [...prev, newRow];
    });
  };

  const handleDeleteRow = (index: number) => {
    if (rows.length <= 1) return;
    setRows((prev) => prev.filter((_, i) => i !== index));
  };

  const applyErrorsAndFocus = (
    errorsByRowIdx: Record<number, Record<string, string>>,
    topLevelErrors: Record<string, string> = {}
  ) => {
    let totalProblems = Object.keys(topLevelErrors).length;
    let failingRows = 0;
    let firstTarget: { row: number; field: string } | null = null;

    const updatedRows = rows.map((row, idx) => {
      const raw = errorsByRowIdx[idx] ?? {};
      const mapped: Record<string, string> = {};
      for (const [k, v] of Object.entries(raw)) {
        const uiKey = category === "water" && k === "waterDepartmentId" ? "departmentId" : k;
        mapped[uiKey] = v;
      }
      const keys = Object.keys(mapped);
      if (keys.length > 0) {
        totalProblems += keys.length;
        failingRows += 1;
        if (!firstTarget) {
          firstTarget = { row: idx, field: keys[0] };
        }
      }
      return { ...row, errors: mapped };
    });

    setRows(updatedRows);

    if (firstTarget) {
      focusNonceRef.current += 1;
      setFocusErrorTarget({ ...(firstTarget as { row: number; field: string }), nonce: focusNonceRef.current });
    }

    if (failingRows > 0) {
      const topMsgs = Object.values(topLevelErrors);
      const summary = formatErrorSummary(totalProblems, failingRows);
      setErrorMessage(topMsgs.length > 0 ? `${summary}\n${topMsgs.join("\n")}` : summary);
    } else if (Object.keys(topLevelErrors).length > 0) {
      setErrorMessage(Object.values(topLevelErrors).join("\n"));
    }
  };

  const validateRows = (): boolean => {
    let isValid = true;
    const rowErrors: Record<number, Record<string, string>> = {};

    rows.forEach((row, idx) => {
      const errors: Record<string, string> = {};

      if (category === "product" || category === "rm" || category === "pm") {
        if (!row.itemId) {
          errors.itemId = "Item is required";
          isValid = false;
        } else if (labMode.kind === "choose" && !(row.targetSectionIds && row.targetSectionIds.length > 0)) {
          errors.targetSectionIds = "Choose at least one laboratory";
          isValid = false;
        }

        if (row.itemId && row.gradeOptions?.length && !row.grade) {
          errors.grade = "Grade is required";
          isValid = false;
        }

        if (category === "product" && (!row.productionStage || row.productionStage.trim() === "")) {
          errors.productionStage = "Production Stage is required";
          isValid = false;
        }
      } else if (category === "water") {
        if (!row.departmentId) {
          errors.departmentId = "Department is required";
          isValid = false;
        }
      } else if (category === "em") {
        if (!row.departmentId) {
          errors.departmentId = "Department is required";
          isValid = false;
        }
      } else if (category === "ac") {
        const isPhyschem = labMode.kind === "fixed" && labMode.code !== "MICRO";
        if (isPhyschem) {
          if (!row.previousProductItemId) {
            errors.previousProductItemId = "Previous Product is required";
            isValid = false;
          }
          if (!row.techniques || row.techniques.length === 0) {
            errors.techniques = "At least one technique is required";
            isValid = false;
          }
        } else {
          if ((!row.previousProductName || row.previousProductName.trim() === "") && !row.previousProductItemId) {
            errors.previousProductName = "Previous Product is required";
            isValid = false;
          }
        }
        if (!row.previousProductBatchNumber || row.previousProductBatchNumber.trim() === "") {
          errors.previousProductBatchNumber = "Previous Product Batch No. is required";
          isValid = false;
        }
        if (!row.machineId) {
          errors.machineId = "Machine is required";
          isValid = false;
        }
      }

      if (!row.causeOfTestingId) {
        errors.causeOfTestingId = "Cause of Testing is required";
        isValid = false;
      }

      if (!row.sampledBy || row.sampledBy.trim() === "") {
        errors.sampledBy = "Sampled By is required";
        isValid = false;
      }

      if (row.mfgDate && row.expDate && row.expDate < row.mfgDate) {
        errors.expDate = "Expiry Date must be after Manufacturing Date";
        isValid = false;
      }

      if (Object.keys(errors).length > 0) {
        rowErrors[idx] = errors;
      }
    });

    if (!isValid) {
      applyErrorsAndFocus(rowErrors);
      setErrorMessage((prev) =>
        prev ? `Please fix validation errors before saving:\n${prev}` : "Please fix validation errors before saving."
      );
    }

    return isValid;
  };

  const buildBatchRequest = (cat: SampleCategoryKey): BatchReceiveRequest => {
    if (cat === "product" || cat === "rm" || cat === "pm") {
      return {
        category: "Product",
        items: rows.map((row) => ({
          itemId: Number(row.itemId),
          causeOfTestingId: Number(row.causeOfTestingId),
          sampleQuantity: row.sampleQuantity || "",
          sampledBy: row.sampledBy || "",
          batchNumber: row.batchNumber || "",
          controlNumber: row.controlNumber || "",
          mfgDate: row.mfgDate || null,
          expDate: row.expDate || null,
          productionStage: cat === "product" ? row.productionStage || null : null,
          targetSectionIds: labMode.kind === "fixed" ? [labMode.sectionId] : row.targetSectionIds || [],
          grade: row.gradeOptions?.length ? row.grade || null : null
        }))
      };
    }
    if (cat === "water") {
      return {
        category: "Water",
        water: rows.map((row) => ({
          waterDepartmentId: Number(row.departmentId),
          causeOfTestingId: Number(row.causeOfTestingId),
          sampleQuantity: row.sampleQuantity || "",
          sampledBy: row.sampledBy || "",
          controlNumber: row.controlNumber || "",
          sampledAt: sampledAtIso(row),
          labSectionId: labMode.kind === "fixed" ? labMode.sectionId : undefined
        }))
      };
    }
    if (cat === "em") {
      return {
        category: "EM",
        em: rows.map((row) => ({
          departmentId: Number(row.departmentId),
          causeOfTestingId: Number(row.causeOfTestingId),
          sampledBy: row.sampledBy || "",
          controlNumber: row.controlNumber || "",
          sampledAt: sampledAtIso(row)
        }))
      };
    }
    const isPhyschem = labMode.kind === "fixed" && labMode.code !== "MICRO";
    return {
      category: "AfterCleaning",
      afterCleaning: rows.map((row) => ({
        machineId: Number(row.machineId),
        causeOfTestingId: Number(row.causeOfTestingId),
        sampledBy: row.sampledBy || "",
        controlNumber: row.controlNumber || "",
        previousProductName: row.previousProductName ? String(row.previousProductName).trim() : null,
        previousProductBatchNumber: String(row.previousProductBatchNumber || "").trim(),
        sampledAt: sampledAtIso(row),
        labSectionId: labMode.kind === "fixed" ? labMode.sectionId : undefined,
        previousProductItemId: row.previousProductItemId ? Number(row.previousProductItemId) : null,
        techniques: isPhyschem && row.techniques ? row.techniques : null
      }))
    };
  };

  const toCreatedRefs = (byRow: Record<number, BatchReceivedSample>): CreatedSampleRef[] =>
    Object.values(byRow)
      .sort((a, b) => a.row - b.row)
      .map((s) => ({ id: s.sampleId, referenceNumber: s.referenceNumber }));

  const handleFinishWithCreated = (byRow: Record<number, BatchReceivedSample>) => {
    onSuccess(toCreatedRefs(byRow));
    onClose();
  };

  const handleSaveAll = async () => {
    if (receivedByRow) {
      handleFinishWithCreated(receivedByRow);
      return;
    }
    setErrorMessage(null);
    if (!category) return;
    if (!validateRows()) return;

    setLoading(true);

    try {
      const result = await ReceiveService.receiveBatch(buildBatchRequest(category));
      const sorted = [...result.samples].sort((a, b) => a.row - b.row);
      const byRow: Record<number, BatchReceivedSample> = {};
      for (const s of sorted) {
        byRow[s.row] = s;
      }

      await draft.onSubmitted();
      setDraftSavedAt(null);

      if (category === "rm") {
        const failedCoas: Record<number, string> = {};
        for (let idx = 0; idx < rows.length; idx += 1) {
          const file = rows[idx]?.coaFile;
          const sample = byRow[idx];
          if (file && sample) {
            try {
              await ReceiveService.uploadSampleDocument(sample.sampleId, file);
            } catch {
              failedCoas[idx] = "Certificate not attached — retry";
            }
          }
        }
        if (Object.keys(failedCoas).length > 0) {
          setReceivedByRow(byRow);
          setCoaErrorByRow(failedCoas);
          const numbers = sorted.map((s) => s.referenceNumber).join(", ");
          setWarnings([
            `Samples received (${numbers}), but one or more supplier certificates could not be attached. Retry on the marked row(s) below.`
          ]);
          setWarningSnackbarOpen(true);
          return;
        }
      }

      handleFinishWithCreated(byRow);
    } catch (err: unknown) {
      const rowErrs = errorsByRow(err);
      const fieldErrs = errorsByField(err);
      if (Object.keys(rowErrs).length > 0 || Object.keys(fieldErrs).length > 0) {
        applyErrorsAndFocus(rowErrs, fieldErrs);
      } else {
        setErrorMessage(`Failed to receive samples: ${getErrorMessage(err, "An unexpected error occurred while saving samples.")}`);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleRetryCoa = async (rowIdx: number) => {
    if (!receivedByRow) return;
    const sample = receivedByRow[rowIdx];
    const file = rows[rowIdx]?.coaFile;
    if (!sample || !file) return;

    setRetryingCoaRow(rowIdx);
    try {
      await ReceiveService.uploadSampleDocument(sample.sampleId, file);
      const nextErrors = { ...coaErrorByRow };
      delete nextErrors[rowIdx];
      setCoaErrorByRow(nextErrors);
      if (Object.keys(nextErrors).length === 0) {
        handleFinishWithCreated(receivedByRow);
      }
    } catch {
      setCoaErrorByRow((prev) => ({
        ...prev,
        [rowIdx]: "Certificate not attached — retry"
      }));
    } finally {
      setRetryingCoaRow(null);
    }
  };

  return (
    <>
      <FloatingDialog
      open={open}
      onClose={() => { if (!loading) onClose(); }}
      maxWidth={step === 1 ? "md" : "xl"}
      paperSx={{ borderRadius: 2.5, p: 0.5 }}
      titleSx={{ pb: 1 }}
      title={
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", flex: 1, minWidth: 0, mr: 1 }}>
          <Box>
            <Typography sx={{ fontSize: 20, fontWeight: 700, color: theme.palette.primary.main }}>
              New Sample
            </Typography>
            <Typography sx={{ fontSize: 13, color: theme.palette.primary.main, fontWeight: 600 }}>
              {step === 1
                ? "Step 1 of 2: Choose Item Type"
                : `Step 2 of 2: Enter Sample Details for ${currentCategoryDef?.label || ""}`}
            </Typography>
          </Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
            Step {step} of 2
          </Typography>
        </Box>
      }
      actions={
        step === 1 ? (
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", width: "100%" }}>
            <Button onClick={onClose} color="inherit">
              Cancel
            </Button>
            <Button
              variant="contained"
              color="primary"
              disabled={!category}
              onClick={handleNextStep}
              endIcon={<ArrowForwardIcon />}
              sx={{
                fontWeight: 600,
                px: 3
              }}
            >
              Next
            </Button>
          </Box>
        ) : (
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", width: "100%" }}>
            <Button
              onClick={handlePrevStep}
              disabled={loading}
              startIcon={<ArrowBackIcon />}
              color="inherit"
            >
              Back
            </Button>

            <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main }}>
                Total Samples: {rows.length}
              </Typography>
              {draftSavedAt && draft.storageAvailable && !receivedByRow && (
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  Draft saved on this computer at {formatHm(draftSavedAt)} — not recorded
                </Typography>
              )}
            </Box>

            <Box sx={{ display: "flex", gap: 1.5 }}>
              <Button onClick={onClose} disabled={loading} color="inherit">
                Cancel
              </Button>
              <Button
                variant="contained"
                color="primary"
                onClick={handleSaveAll}
                disabled={loading}
                startIcon={loading ? <CircularProgress size={18} color="inherit" /> : <CheckIcon />}
                sx={{
                  fontWeight: 600,
                  px: 3
                }}
              >
                {receivedByRow ? "Done" : loading ? "Receiving..." : "Save All Samples"}
              </Button>
            </Box>
          </Box>
        )
      }
    >
        {errorMessage && (
          <Alert severity="error" aria-live="polite" sx={{ mb: 2, whiteSpace: "pre-line", fontSize: 13 }}>
            {errorMessage}
          </Alert>
        )}

        {step === 2 && !receivedByRow && (
          <DraftRestoreNotice
            startedAt={draft.restoredStartedAt}
            onRestore={() => {
              const restored = draft.acceptRestore();
              if (restored?.rows && restored.rows.length > 0) {
                setRows(restored.rows.map((r) => ({ ...createEmptyRow(), ...r, coaFile: null, errors: {} })));
              }
            }}
            onDiscard={() => {
              setDraftSavedAt(null);
              void draft.discard();
            }}
          />
        )}

        {step === 1 ? (
          <SampleTypeSelector
            selectedCategory={category}
            allowedCategories={allowedCategories}
            onSelectCategory={(cat) => {
              setCategory(cat);
              setErrorMessage(null);
            }}
          />
        ) : (
          category && (
            <MultiSampleEntryGrid
              category={category}
              rows={rows}
              masterData={masterData}
              labMode={labMode}
              onChangeRow={handleChangeRow}
              onAddRow={handleAddRow}
              onDeleteRow={handleDeleteRow}
              onSave={handleSaveAll}
              focusErrorTarget={focusErrorTarget}
              coaErrorByRow={coaErrorByRow}
              retryingCoaRow={retryingCoaRow}
              onRetryCoa={handleRetryCoa}
              receivedByRow={receivedByRow ?? undefined}
              firstRowDefaults={{
                causeOfTestingId: causeLastChoice.isDefault,
                sampledBy: sampledByLastChoice.isDefault
              }}
            />
          )
        )}
      </FloatingDialog>
      <Snackbar
        open={warningSnackbarOpen}
        autoHideDuration={8000}
        onClose={() => setWarningSnackbarOpen(false)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert
          severity="warning"
          onClose={() => setWarningSnackbarOpen(false)}
          sx={{ borderRadius: 1.5, whiteSpace: "pre-line", maxWidth: 600 }}
        >
          {warnings.join("\n")}
        </Alert>
      </Snackbar>
    </>
  );
}
