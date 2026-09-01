import { useEffect, useState } from "react";
import { Alert, Box, CircularProgress } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { TestPreparationForm } from "./TestPreparationForm";
import { ConfirmPreparationForm } from "./ConfirmPreparationForm";
import { EMPreparationForm } from "../laboratoryConfiguration/environmentalMonitoring/EMPreparationForm";
import { AfterCleaningPreparationForm } from "../laboratoryConfiguration/afterCleaning/AfterCleaningPreparationForm";
import { WaterPreparationForm } from "../laboratoryConfiguration/water/WaterPreparationForm";
import {
  ItemPreparationConfigurationService,
  type ItemPreparationConfiguration
} from "./services/ItemPreparationConfigurationService";

interface Props {
  open: boolean;
  sample: {
    sampleId: number;
    category: string;
    itemId?: number | null;
    itemName?: string | null;
    departmentId?: number | null;
    machineId?: number | null;
    waterDepartmentId?: number | null;
    assignedAnalystId?: number | null;
    assignedAnalystName?: string | null;
  } | null;
  onClose: () => void;
}

// Product/RM/PM route through the item's preparation configuration; the
// batch categories keep their own location-selection forms untouched.
function isItemCategory(category: string) {
  return category !== "EnvironmentalMonitoring" && category !== "AfterCleaning" && category !== "Water";
}

// Opened directly from a Testing Workspace card when a sample "Needs
// Preparation" - routes to the right form by category, reusing the same
// forms the standalone EM/After Cleaning/Water/Test Preparation pages use.
export function PreparationDialog({ open, sample, onClose }: Props) {
  const [config, setConfig] = useState<ItemPreparationConfiguration | null>(null);
  const [loadingConfig, setLoadingConfig] = useState(false);
  const [configError, setConfigError] = useState<string | null>(null);

  const itemId = sample && isItemCategory(sample.category) ? sample.itemId ?? null : null;

  useEffect(() => {
    if (!open || itemId == null) {
      setConfig(null);
      setConfigError(null);
      return;
    }

    let cancelled = false;
    setLoadingConfig(true);
    setConfigError(null);

    ItemPreparationConfigurationService.get(itemId)
      .then((c) => { if (!cancelled) setConfig(c); })
      .catch((e: any) => {
        if (!cancelled) setConfigError(e?.response?.data?.message ?? "Could not load this item's preparation configuration.");
      })
      .finally(() => { if (!cancelled) setLoadingConfig(false); });

    return () => { cancelled = true; };
  }, [open, itemId]);

  if (!sample) return null;

  return (
    <FloatingDialog open={open} title="Preparation" onClose={onClose}>
      {sample.category === "EnvironmentalMonitoring" && sample.departmentId != null && (
        <EMPreparationForm sampleId={sample.sampleId} departmentId={sample.departmentId} onComplete={onClose} />
      )}
      {sample.category === "AfterCleaning" && sample.machineId != null && (
        <AfterCleaningPreparationForm sampleId={sample.sampleId} machineId={sample.machineId} onComplete={onClose} />
      )}
      {sample.category === "Water" && sample.waterDepartmentId != null && (
        <WaterPreparationForm sampleId={sample.sampleId} waterDepartmentId={sample.waterDepartmentId} onComplete={onClose} />
      )}

      {isItemCategory(sample.category) && (
        <>
          {loadingConfig && (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
              <CircularProgress size={28} />
            </Box>
          )}

          {!loadingConfig && configError && <Alert severity="error">{configError}</Alert>}

          {/* A configuration in any approval state routes to confirm-only;
              only its total absence falls back to manual entry. */}
          {!loadingConfig && !configError && config && (
            <ConfirmPreparationForm sample={sample} config={config} onSaved={onClose} />
          )}

          {!loadingConfig && !configError && !config && (
            <TestPreparationForm sample={sample} onSaved={onClose} />
          )}
        </>
      )}
    </FloatingDialog>
  );
}
