import { useState, useEffect, useCallback } from "react";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { SolutionPreparationService } from "../services/SolutionPreparationService";
import type {
  SolutionPreparationResponse,
  TitrantStandardizationResponse
} from "../types";

export function useTitrantStandardization(
  preparation: SolutionPreparationResponse | null,
  onRefreshPreparation: (id: number) => Promise<void>
) {
  const { permissions } = useAuth();
  const [standardizations, setStandardizations] = useState<TitrantStandardizationResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);

  const prepId = preparation?.id;
  const isTitrant = preparation?.type === "Titrant";

  const loadStandardizations = useCallback(async (id: number) => {
    setLoading(true);
    try {
      const data = await SolutionPreparationService.getStandardizations(id);
      setStandardizations(data);
    } catch {
      setStandardizations([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (prepId && isTitrant) {
      loadStandardizations(prepId);
    }
  }, [prepId, isTitrant, loadStandardizations]);

  const canStandardize = Boolean(
    isTitrant &&
    preparation?.effectiveStatus === "Prepared" &&
    permissions.includes(PERMISSIONS.SOLUTIONS_PREPARE)
  );

  // The dialog stays open on its result view; the refresh is silent so it
  // does not unmount the dialog and bring back the empty form.
  const handleSuccess = useCallback(async () => {
    if (prepId) {
      await Promise.all([
        onRefreshPreparation(prepId),
        loadStandardizations(prepId)
      ]);
    }
    toast.success("Titrant standardized successfully.");
  }, [prepId, onRefreshPreparation, loadStandardizations]);

  return {
    standardizations,
    loadingStandardizations: loading,
    canStandardize,
    dialogOpen,
    setDialogOpen,
    handleSuccess,
    reloadStandardizations: () => prepId && loadStandardizations(prepId)
  };
}
