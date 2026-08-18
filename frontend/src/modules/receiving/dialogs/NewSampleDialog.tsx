import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Alert,
  CircularProgress
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CheckIcon from "@mui/icons-material/Check";
import { ReceiveRowItem, SampleCategoryKey } from "../types/receivingTypes";
import { RECEIVING_CATEGORIES } from "../constants/receivingConstants";
import { SampleTypeSelector } from "./SampleTypeSelector";
import { MultiSampleEntryGrid } from "./MultiSampleEntryGrid";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { brandColors } from "../../../theme";

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (count: number) => void;
}

const createEmptyRow = (defaultValues?: Partial<ReceiveRowItem>): ReceiveRowItem => ({
  id: Math.random().toString(36).substring(2, 9),
  itemId: defaultValues?.itemId ?? "",
  productionStage: defaultValues?.productionStage ?? "",
  waterSamplingPointId: defaultValues?.waterSamplingPointId ?? "",
  departmentId: defaultValues?.departmentId ?? "",
  machineId: defaultValues?.machineId ?? "",
  causeOfTestingId: defaultValues?.causeOfTestingId ?? "",
  sampleQuantity: defaultValues?.sampleQuantity ?? "",
  sampledBy: defaultValues?.sampledBy ?? "",
  batchNumber: defaultValues?.batchNumber ?? "",
  controlNumber: defaultValues?.controlNumber ?? "",
  mfgDate: defaultValues?.mfgDate ?? "",
  expDate: defaultValues?.expDate ?? "",
  errors: {}
});

export function NewSampleDialog({ open, onClose, onSuccess }: Props) {
  const [step, setStep] = useState<1 | 2>(1);
  const [category, setCategory] = useState<SampleCategoryKey | null>(null);
  const [rows, setRows] = useState<ReceiveRowItem[]>([createEmptyRow()]);

  const [masterData, setMasterData] = useState<{
    items: any[];
    waterPoints: any[];
    waterDepartments: any[];
    departments: any[];
    machines: any[];
    causes: any[];
  }>({
    items: [],
    waterPoints: [],
    waterDepartments: [],
    departments: [],
    machines: [],
    causes: []
  });

  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Load shared master data when dialog opens
  useEffect(() => {
    if (open) {
      setStep(1);
      setCategory(null);
      setRows([createEmptyRow()]);
      setErrorMessage(null);

      masterDataOptions.getCausesOfTesting().then((causes) =>
        setMasterData((prev) => ({ ...prev, causes }))
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

  // Load items when item-based category is selected
  useEffect(() => {
    if (category) {
      const catDef = RECEIVING_CATEGORIES.find((c) => c.key === category);
      if (catDef?.apiCategory) {
        masterDataOptions.getItems(catDef.apiCategory).then((items) =>
          setMasterData((prev) => ({ ...prev, items }))
        );
      }
    }
  }, [category]);

  const handleNextStep = () => {
    if (!category) return;
    setStep(2);
    setRows([createEmptyRow()]);
    setErrorMessage(null);
  };

  const handlePrevStep = () => {
    setStep(1);
    setErrorMessage(null);
  };

  const handleChangeRow = (index: number, field: string, value: any) => {
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
  };

  const handleAddRow = () => {
    setRows((prev) => {
      // Intelligently prefill common fields (sampledBy, causeOfTestingId) from last row
      const lastRow = prev[prev.length - 1];
      const newRow = createEmptyRow({
        sampledBy: lastRow?.sampledBy || "",
        causeOfTestingId: lastRow?.causeOfTestingId || ""
      });
      return [...prev, newRow];
    });
  };

  const handleDeleteRow = (index: number) => {
    if (rows.length <= 1) return;
    setRows((prev) => prev.filter((_, i) => i !== index));
  };

  const validateRows = (): boolean => {
    let isValid = true;
    const errorSummaries: string[] = [];

    const validatedRows = rows.map((row, idx) => {
      const errors: Record<string, string> = {};

      if (category === "product" || category === "rm" || category === "pm") {
        if (!row.itemId) {
          errors.itemId = "Item is required";
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
        errorSummaries.push(`Row ${idx + 1}: ${Object.values(errors).join(", ")}`);
      }

      return { ...row, errors };
    });

    setRows(validatedRows);

    if (!isValid) {
      setErrorMessage(`Please fix validation errors before saving:\n${errorSummaries.join("\n")}`);
    }

    return isValid;
  };

  const handleSaveAll = async () => {
    setErrorMessage(null);
    if (!category) return;
    if (!validateRows()) return;

    setLoading(true);

    try {
      // Process each row sequentially or in parallel
      for (const row of rows) {
        if (category === "product" || category === "rm" || category === "pm") {
          await ReceiveService.receiveItemBased({
            itemId: Number(row.itemId),
            causeOfTestingId: Number(row.causeOfTestingId),
            sampleQuantity: row.sampleQuantity || "",
            sampledBy: row.sampledBy || "",
            batchNumber: row.batchNumber || "",
            controlNumber: row.controlNumber || "",
            mfgDate: row.mfgDate || null,
            expDate: row.expDate || null,
            productionStage: category === "product" ? row.productionStage || null : null
          });
        } else if (category === "water") {
          await ReceiveService.receiveWater({
            waterDepartmentId: Number(row.departmentId),
            causeOfTestingId: Number(row.causeOfTestingId),
            sampleQuantity: row.sampleQuantity || "",
            sampledBy: row.sampledBy || "",
            controlNumber: row.controlNumber || ""
          });
        } else if (category === "em") {
          await ReceiveService.receiveEM({
            departmentId: Number(row.departmentId),
            causeOfTestingId: Number(row.causeOfTestingId),
            sampledBy: row.sampledBy || "",
            controlNumber: row.controlNumber || ""
          });
        } else if (category === "ac") {
          await ReceiveService.receiveAfterCleaning({
            machineId: Number(row.machineId),
            causeOfTestingId: Number(row.causeOfTestingId),
            sampledBy: row.sampledBy || "",
            controlNumber: row.controlNumber || ""
          });
        }
      }

      onSuccess(rows.length);
      onClose();
    } catch (err: any) {
      const serverMsg =
        err?.response?.data?.message ||
        err?.message ||
        "An unexpected error occurred while saving samples.";
      setErrorMessage(`Failed to receive samples: ${serverMsg}`);
    } finally {
      setLoading(false);
    }
  };

  const currentCategoryDef = RECEIVING_CATEGORIES.find((c) => c.key === category);

  return (
    <Dialog
      open={open}
      onClose={loading ? undefined : onClose}
      maxWidth={step === 1 ? "md" : "xl"}
      fullWidth
      PaperProps={{
        sx: {
          borderRadius: 2.5,
          p: 0.5
        }
      }}
    >
      <DialogTitle sx={{ pb: 1 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
          <Box>
            <Typography sx={{ fontSize: 20, fontWeight: 700, color: brandColors.pageTitle }}>
              New Sample
            </Typography>
            <Typography sx={{ fontSize: 13, color: brandColors.sectionTitle, fontWeight: 600 }}>
              {step === 1
                ? "Step 1 of 2: Choose Item Type"
                : `Step 2 of 2: Enter Sample Details — ${currentCategoryDef?.label || ""}`}
            </Typography>
          </Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
            Step {step} of 2
          </Typography>
        </Box>
      </DialogTitle>

      <DialogContent sx={{ pb: 2 }}>
        {errorMessage && (
          <Alert severity="error" sx={{ mb: 2, whiteSpace: "pre-line", fontSize: 13 }}>
            {errorMessage}
          </Alert>
        )}

        {step === 1 ? (
          <SampleTypeSelector
            selectedCategory={category}
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
              onChangeRow={handleChangeRow}
              onAddRow={handleAddRow}
              onDeleteRow={handleDeleteRow}
            />
          )
        )}
      </DialogContent>

      <DialogActions
        sx={{
          px: 3,
          py: 2,
          borderTop: "1px solid #e5e7eb",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center"
        }}
      >
        {step === 1 ? (
          <>
            <Button onClick={onClose} color="inherit" sx={{ color: "#4b5563" }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              disabled={!category}
              onClick={handleNextStep}
              endIcon={<ArrowForwardIcon />}
              sx={{
                bgcolor: brandColors.sectionTitle,
                fontWeight: 600,
                px: 3,
                "&:hover": { bgcolor: "#631f74" }
              }}
            >
              Next
            </Button>
          </>
        ) : (
          <>
            <Button
              onClick={handlePrevStep}
              disabled={loading}
              startIcon={<ArrowBackIcon />}
              color="inherit"
              sx={{ color: "#4b5563" }}
            >
              Back
            </Button>

            <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle }}>
              Total Samples: {rows.length}
            </Typography>

            <Box sx={{ display: "flex", gap: 1.5 }}>
              <Button onClick={onClose} disabled={loading} color="inherit" sx={{ color: "#4b5563" }}>
                Cancel
              </Button>
              <Button
                variant="contained"
                onClick={handleSaveAll}
                disabled={loading}
                startIcon={loading ? <CircularProgress size={18} color="inherit" /> : <CheckIcon />}
                sx={{
                  bgcolor: brandColors.sectionTitle,
                  fontWeight: 600,
                  px: 3,
                  "&:hover": { bgcolor: "#631f74" }
                }}
              >
                {loading ? "Receiving..." : "Save All Samples"}
              </Button>
            </Box>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
