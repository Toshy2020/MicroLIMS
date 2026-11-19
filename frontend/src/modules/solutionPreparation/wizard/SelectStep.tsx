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
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { getErrorMessage } from "../../../utils/errorMessage";
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
  const { t } = useTranslation("solutionPreparation");
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
        setError(getErrorMessage(err, t("wizard.selectStep.loadSolutionsError")));
      })
      .finally(() => {
        if (active) setSolutionsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [type, initialSolutionId, initialMethodId, t]);

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
        setError(getErrorMessage(err, t("wizard.selectStep.loadMethodsError")));
      })
      .finally(() => {
        if (active) setMethodsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [type, selectedSolutionId, initialMethodId, t]);

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
          {t("wizard.selectStep.heading")}
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Stack spacing={2.5}>
          <FormControl fullWidth size="small">
            <InputLabel id="solution-type-label">{t("wizard.selectStep.solutionType")}</InputLabel>
            <Select
              labelId="solution-type-label"
              value={type}
              label={t("wizard.selectStep.solutionType")}
              onChange={(e) => setType(e.target.value as SolutionType)}
            >
              <MenuItem value="MobilePhase">{t("solutionTypes.MobilePhase")}</MenuItem>
              <MenuItem value="Diluent">{t("solutionTypes.Diluent")}</MenuItem>
              <MenuItem value="Reagent">{t("solutionTypes.Reagent")}</MenuItem>
              <MenuItem value="Neutralizer">{t("solutionTypes.Neutralizer")}</MenuItem>
              <MenuItem value="Titrant">{t("solutionTypes.Titrant")}</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small" disabled={solutionsLoading}>
            <InputLabel id="solution-select-label">{t("wizard.selectStep.solutionRecipe")}</InputLabel>
            <Select
              labelId="solution-select-label"
              value={selectedSolutionId}
              label={t("wizard.selectStep.solutionRecipe")}
              onChange={(e) => setSelectedSolutionId(e.target.value as number)}
            >
              <MenuItem value="">
                <em>{t("wizard.selectStep.chooseRecipe")}</em>
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
              <InputLabel id="method-select-label">{t("wizard.selectStep.hplcMethod")}</InputLabel>
              <Select
                labelId="method-select-label"
                value={selectedMethodId}
                label={t("wizard.selectStep.hplcMethod")}
                onChange={(e) => setSelectedMethodId(e.target.value as number)}
              >
                <MenuItem value="">
                  <em>{t("wizard.selectStep.chooseMethod")}</em>
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
                    {t("wizard.selectStep.checkingMethods")}
                  </Typography>
                </Box>
              ) : methods.length === 0 ? (
                <Typography variant="caption" sx={{ color: "error.main", mt: 0.5 }}>
                  {t("wizard.selectStep.noMethods")}
                </Typography>
              ) : null}
            </FormControl>
          )}

          {selectedSolution && (
            <Box sx={{ mt: 1, p: 2, bgcolor: "action.hover", borderRadius: 1 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                {t("wizard.selectStep.recipeOverview", { name: selectedSolution.name })}
              </Typography>
              <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
                <Chip
                  size="small"
                  label={<Trans i18nKey="wizard.selectStep.targetVolume" ns="solutionPreparation" values={{ value: selectedSolution.finalVolumeMl }} components={{ ltr: <Ltr>{null}</Ltr> }} />}
                />
                <Chip
                  size="small"
                  label={<Trans i18nKey="wizard.selectStep.shelfLife" ns="solutionPreparation" values={{ value: selectedSolution.shelfLifeValue, unit: selectedSolution.shelfLifeUnit }} components={{ ltr: <Ltr>{null}</Ltr> }} />}
                />
                <Chip size="small" label={t("wizard.selectStep.storage", { condition: selectedSolution.storageCondition })} />
                {selectedSolution.sterilizationRequired && (
                  <Chip size="small" variant="outlined" label={t("wizard.selectStep.autoclaveRequired")} />
                )}
                {selectedSolution.releaseRequired && (
                  <Chip size="small" variant="outlined" label={t("wizard.selectStep.releaseRequired")} />
                )}
                {selectedSolution.phTarget != null && (
                  <Chip
                    size="small"
                    color="primary"
                    variant="outlined"
                    label={
                      <Trans
                        i18nKey={selectedSolution.phTolerance ? "wizard.selectStep.phChipTolerance" : "wizard.selectStep.phChip"}
                        ns="solutionPreparation"
                        values={{ target: selectedSolution.phTarget, tolerance: selectedSolution.phTolerance }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    }
                  />
                )}
              </Box>

              <Divider sx={{ my: 1.5 }} />

              <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                <Trans
                  i18nKey="wizard.selectStep.componentsCount"
                  ns="solutionPreparation"
                  values={{ count: selectedSolution.components?.length ?? 0 }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              <Stack spacing={0.5}>
                {selectedSolution.components?.map((c) => (
                  <Typography key={c.id ?? c.order} variant="caption" sx={{ color: "text.secondary" }}>
                    {c.kind === "Text"
                      ? `• ${c.text}`
                      : c.kind === "PreparedSolution"
                        ? t("wizard.selectStep.preparedComponentLine", { name: c.sourceSolutionName, qty: c.quantity, unit: c.unit })
                        : t("wizard.selectStep.materialComponentLine", { name: c.entryName, code: c.entryCode, qty: c.quantity, unit: c.unit })}
                  </Typography>
                ))}
              </Stack>

              {selectedSolution.instructions && (
                <>
                  <Divider sx={{ my: 1.5 }} />
                  <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                    {t("wizard.selectStep.instructionsHeading")}
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
              {loading ? t("wizard.selectStep.starting") : t("wizard.selectStep.startPreparation")}
            </Button>
          </Box>
        </Stack>
      </Paper>
    </Stack>
  );
}
