import { useState, useEffect } from "react";
import {
  Box,
  Paper,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Button,
  Alert,
  CircularProgress,
  Stack,
  Divider,
  Chip
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import {
  SolutionMasterService,
  SolutionMaster
} from "../../laboratoryConfiguration/masterDataSimple/services/SolutionMasterService";
import {
  HplcMethodService,
  HplcMethodResponse
} from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import type { SolutionType } from "../types";

interface Props {
  initialType?: SolutionType;
  initialSolutionId?: number | null;
  initialMethodId?: number | null;
  onStart: (solutionMasterId: number, hplcMethodId?: number | null) => Promise<void>;
  loading?: boolean;
}

export function SelectStep({
  initialType = "MobilePhase",
  initialSolutionId = null,
  initialMethodId = null,
  onStart,
  loading = false
}: Props) {
  const [type, setType] = useState<SolutionType>(initialType);
  const [solutions, setSolutions] = useState<SolutionMaster[]>([]);
  const [selectedSolutionId, setSelectedSolutionId] = useState<number | "">(initialSolutionId ?? "");
  const [solutionsLoading, setSolutionsLoading] = useState(false);

  const [methods, setMethods] = useState<HplcMethodResponse[]>([]);
  const [selectedMethodId, setSelectedMethodId] = useState<number | "">(initialMethodId ?? "");
  const [methodsLoading, setMethodsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load solutions when type changes
  useEffect(() => {
    let active = true;
    setSolutionsLoading(true);
    setError(null);
    setSelectedSolutionId(initialSolutionId ?? "");
    setSelectedMethodId(initialMethodId ?? "");

    SolutionMasterService.getAll(type, true)
      .then((data) => {
        if (!active) return;
        setSolutions(data);
      })
      .catch((err: unknown) => {
        if (!active) return;
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        setError(e.response?.data?.message ?? e.message ?? "Failed to load solutions.");
      })
      .finally(() => {
        if (active) setSolutionsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [type, initialSolutionId, initialMethodId]);

  // When a mobile phase solution is selected, load methods that use it
  useEffect(() => {
    if (type !== "MobilePhase" || !selectedSolutionId) {
      setMethods([]);
      setSelectedMethodId("");
      return;
    }

    let active = true;
    setMethodsLoading(true);

    HplcMethodService.getAll(true)
      .then(async (list) => {
        // Fetch details for active methods to find which use this mobile phase
        const detailPromises = list.map((m) =>
          HplcMethodService.getById(m.id).catch(() => null)
        );
        const details = await Promise.all(detailPromises);
        if (!active) return;

        const filtered = details.filter(
          (d): d is HplcMethodResponse =>
            d !== null &&
            d.mobilePhases.some((mp) => mp.solutionMasterId === Number(selectedSolutionId))
        );
        setMethods(filtered);

        // Keep initialMethodId if still valid, otherwise reset
        if (initialMethodId && filtered.some((m) => m.id === initialMethodId)) {
          setSelectedMethodId(initialMethodId);
        } else if (filtered.length === 1) {
          setSelectedMethodId(filtered[0].id);
        } else {
          setSelectedMethodId("");
        }
      })
      .catch((err: unknown) => {
        if (!active) return;
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        setError(e.response?.data?.message ?? e.message ?? "Failed to load methods.");
      })
      .finally(() => {
        if (active) setMethodsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [type, selectedSolutionId, initialMethodId]);

  const selectedSolution = solutions.find((s) => s.id === selectedSolutionId);

  const handleSubmit = async () => {
    if (!selectedSolutionId) return;
    if (type === "MobilePhase" && !selectedMethodId) return;
    await onStart(
      Number(selectedSolutionId),
      type === "MobilePhase" ? Number(selectedMethodId) : null
    );
  };

  const isStartDisabled =
    loading ||
    !selectedSolutionId ||
    (type === "MobilePhase" && !selectedMethodId);

  return (
    <Stack spacing={3}>
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 2 }}>
          Step 1: Select Solution & Method
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Stack spacing={2.5}>
          <FormControl fullWidth size="small">
            <InputLabel id="solution-type-label">Solution Type</InputLabel>
            <Select
              labelId="solution-type-label"
              value={type}
              label="Solution Type"
              onChange={(e) => setType(e.target.value as SolutionType)}
            >
              <MenuItem value="MobilePhase">Mobile Phase</MenuItem>
              <MenuItem value="Diluent">Diluent</MenuItem>
              <MenuItem value="Titrant">Titrant</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small" disabled={solutionsLoading}>
            <InputLabel id="solution-select-label">Solution Recipe *</InputLabel>
            <Select
              labelId="solution-select-label"
              value={selectedSolutionId}
              label="Solution Recipe *"
              onChange={(e) => setSelectedSolutionId(e.target.value as number)}
            >
              <MenuItem value="">
                <em>(Choose solution recipe)</em>
              </MenuItem>
              {solutions.map((sol) => (
                <MenuItem key={sol.id} value={sol.id}>
                  {sol.name} ({sol.shelfLifeValue} {sol.shelfLifeUnit})
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {type === "MobilePhase" && selectedSolutionId && (
            <FormControl fullWidth size="small" disabled={methodsLoading}>
              <InputLabel id="method-select-label">HPLC Method *</InputLabel>
              <Select
                labelId="method-select-label"
                value={selectedMethodId}
                label="HPLC Method *"
                onChange={(e) => setSelectedMethodId(e.target.value as number)}
              >
                <MenuItem value="">
                  <em>(Choose HPLC method)</em>
                </MenuItem>
                {methods.map((method) => (
                  <MenuItem key={method.id} value={method.id}>
                    {method.name} ({method.abbreviation})
                  </MenuItem>
                ))}
              </Select>
              {methodsLoading ? (
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, mt: 0.5 }}>
                  <CircularProgress size={14} />
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    Checking HPLC methods using this mobile phase...
                  </Typography>
                </Box>
              ) : methods.length === 0 ? (
                <Typography variant="caption" sx={{ color: "error.main", mt: 0.5 }}>
                  No active HPLC methods use this mobile phase.
                </Typography>
              ) : null}
            </FormControl>
          )}

          {selectedSolution && (
            <Box sx={{ mt: 1, p: 2, bgcolor: "action.hover", borderRadius: 1 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                Recipe Overview: {selectedSolution.name}
              </Typography>
              <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
                <Chip size="small" label={`Target Volume: ${selectedSolution.finalVolumeMl} mL`} />
                <Chip size="small" label={`Shelf Life: ${selectedSolution.shelfLifeValue} ${selectedSolution.shelfLifeUnit}`} />
                <Chip size="small" label={`Storage: ${selectedSolution.storageCondition}`} />
                {selectedSolution.phTarget != null && (
                  <Chip
                    size="small"
                    color="primary"
                    variant="outlined"
                    label={`pH: ${selectedSolution.phTarget}${selectedSolution.phTolerance ? ` ± ${selectedSolution.phTolerance}` : ""}`}
                  />
                )}
              </Box>

              <Divider sx={{ my: 1.5 }} />

              <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                Components ({selectedSolution.components?.length ?? 0}):
              </Typography>
              <Stack spacing={0.5}>
                {selectedSolution.components?.map((c) => (
                  <Typography key={c.id ?? c.order} variant="caption" sx={{ color: "text.secondary" }}>
                    • {c.entryName} ({c.entryCode}) — {c.quantity} {c.unit}
                  </Typography>
                ))}
              </Stack>

              {selectedSolution.instructions && (
                <>
                  <Divider sx={{ my: 1.5 }} />
                  <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                    Preparation Instructions:
                  </Typography>
                  <Typography variant="caption" sx={{ color: "text.secondary", whiteSpace: "pre-wrap" }}>
                    {selectedSolution.instructions}
                  </Typography>
                </>
              )}
            </Box>
          )}

          <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 2 }}>
            <Button
              variant="contained"
              onClick={handleSubmit}
              disabled={isStartDisabled}
              endIcon={loading ? <CircularProgress size={16} /> : <ArrowForwardIcon />}
              sx={{ textTransform: "none", fontWeight: 700, px: 3 }}
            >
              {loading ? "Starting..." : "Start Preparation"}
            </Button>
          </Box>
        </Stack>
      </Paper>
    </Stack>
  );
}
