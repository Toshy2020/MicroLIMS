import { useMemo } from "react";
import {
  Box,
  Paper,
  Typography,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Button,
  Alert,
  Stack,
  Divider,
  Chip,
  useTheme
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot
} from "../types";
import { solutionTypeLabel } from "../types";

interface Props {
  preparation: SolutionPreparationResponse;
  onBack: () => void;
  onSign: () => void;
  signing?: boolean;
}

export function ReviewStep({
  preparation,
  onBack,
  onSign,
  signing = false
}: Props) {
  const theme = useTheme();
  const { t } = useTranslation("solutionPreparation");

  const recipe = useMemo<RecipeSnapshot | null>(() => {
    try {
      return JSON.parse(preparation.recipeSnapshotJson) as RecipeSnapshot;
    } catch {
      return null;
    }
  }, [preparation.recipeSnapshotJson]);

  // Validation rules matching backend CompleteAsync
  const validationErrors = useMemo<string[]>(() => {
    const errors: string[] = [];

    // All components must have lot and quantity
    for (const c of preparation.components) {
      if (c.kind === "Text") continue;
      if (c.kind === "PreparedSolution") {
        if (!c.sourcePreparationId) {
          errors.push(t("wizard.reviewStep.validation.missingPreparedSolution", { name: c.entryName }));
        }
        continue;
      }
      if (!c.materialId || !c.lotBatchNumber) {
        errors.push(t("wizard.reviewStep.validation.missingStockLot", { name: c.entryName }));
      } else if (!c.quantityUsed || c.quantityUsed <= 0) {
        errors.push(t("wizard.reviewStep.validation.missingQuantity", { name: c.entryName }));
      }
    }

    // Final volume required
    if (!preparation.finalVolumeMl || preparation.finalVolumeMl <= 0) {
      errors.push(t("wizard.reviewStep.validation.finalVolumeRequired"));
    }

    // pH check
    if (recipe?.phTarget != null) {
      if (preparation.measuredPh == null) {
        errors.push(t("wizard.reviewStep.validation.measuredPhRequired"));
      } else if (recipe.phTolerance != null) {
        const diff = Math.abs(preparation.measuredPh - recipe.phTarget);
        if (diff > recipe.phTolerance) {
          errors.push(
            t("wizard.reviewStep.validation.phOutOfTolerance", {
              value: preparation.measuredPh,
              target: recipe.phTarget,
              tolerance: recipe.phTolerance
            })
          );
        }
      }
    }

    return errors;
  }, [preparation, recipe, t]);

  const canProceed = validationErrors.length === 0 && !signing;

  return (
    <Stack spacing={3}>
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
          {t("wizard.reviewStep.heading")}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 3 }}>
          {t("wizard.reviewStep.subtitle")}
        </Typography>

        {validationErrors.length > 0 ? (
          <Alert severity="error" icon={<ErrorOutlinedIcon />} sx={{ mb: 3 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
              {t("wizard.reviewStep.cannotComplete")}
            </Typography>
            <Stack spacing={0.5} component="ul" sx={{ pl: 2, m: 0 }}>
              {validationErrors.map((err, idx) => (
                <Typography key={idx} component="li" variant="caption">
                  {err}
                </Typography>
              ))}
            </Stack>
          </Alert>
        ) : (
          <Alert severity="success" icon={<CheckCircleOutlinedIcon />} sx={{ mb: 3 }}>
            {t("wizard.reviewStep.ready")}
          </Alert>
        )}

        {/* General Meta */}
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(4, 1fr)" }, gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("wizard.reviewStep.meta.solutionRecipe")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {preparation.solutionMasterName}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("wizard.reviewStep.meta.type")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {solutionTypeLabel(preparation.type, t)}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("wizard.reviewStep.meta.hplcMethod")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.hplcMethodAbbreviation ? <Ltr>{preparation.hplcMethodAbbreviation}</Ltr> : "—"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("wizard.reviewStep.meta.laboratorySection")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.sectionName || "—"}
            </Typography>
          </Box>
        </Box>

        <Divider sx={{ mb: 2.5 }} />

        {/* Quantities & Parameters */}
        <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap", mb: 3 }}>
          <Chip
            label={
              <Trans i18nKey="wizard.reviewStep.finalVolumeChip" ns="solutionPreparation" values={{ value: preparation.finalVolumeMl ?? "—" }} components={{ ltr: <Ltr>{null}</Ltr> }} />
            }
            color="primary"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
          {preparation.measuredPh != null && (
            <Chip
              label={
                recipe?.phTarget != null
                  ? <Trans i18nKey="wizard.reviewStep.measuredPhTargetChip" ns="solutionPreparation" values={{ value: preparation.measuredPh, target: recipe.phTarget, tolerance: recipe.phTolerance ?? 0 }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                  : <Trans i18nKey="wizard.reviewStep.measuredPhChip" ns="solutionPreparation" values={{ value: preparation.measuredPh }} components={{ ltr: <Ltr>{null}</Ltr> }} />
              }
              color={recipe?.phTarget != null && recipe.phTolerance != null && Math.abs(preparation.measuredPh - recipe.phTarget) > recipe.phTolerance ? "error" : "default"}
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
          {recipe?.releaseRequired && (
            <Chip label={t("wizard.reviewStep.releaseRequired")} variant="outlined" color="warning" sx={{ fontWeight: 600 }} />
          )}
          {recipe?.shelfLifeValue != null && (
            <Chip
              label={
                <Trans i18nKey="wizard.reviewStep.shelfLifeChip" ns="solutionPreparation" values={{ value: recipe.shelfLifeValue, unit: recipe.shelfLifeUnit }} components={{ ltr: <Ltr>{null}</Ltr> }} />
              }
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
        </Box>

        {recipe?.sterilizationRequired && (
          <Box sx={{ mb: 3 }} data-testid="review-autoclave">
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
              {t("wizard.reviewStep.autoclaveHeading")}
            </Typography>
            {preparation.autoclaveEquipmentId ? (
              <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
                <Chip variant="outlined" label={t("wizard.reviewStep.autoclaveChip", { value: preparation.autoclaveEquipmentName ?? `#${preparation.autoclaveEquipmentId}` })} />
                <Chip variant="outlined" label={t("wizard.reviewStep.programChip", { value: preparation.autoclaveProgram ?? "—" })} />
                <Chip variant="outlined" label={t("wizard.reviewStep.loadChip", { value: preparation.loadType ?? "—" })} />
                <Chip variant="outlined" label={<Trans i18nKey="wizard.reviewStep.temperatureChip" ns="solutionPreparation" values={{ value: preparation.autoclaveTemperature ?? "—" }} components={{ ltr: <Ltr>{null}</Ltr> }} />} />
                <Chip variant="outlined" label={<Trans i18nKey="wizard.reviewStep.cycleTimeChip" ns="solutionPreparation" values={{ value: preparation.autoclaveCycleTime ?? "—" }} components={{ ltr: <Ltr>{null}</Ltr> }} />} />
                <Chip variant="outlined" label={<Trans i18nKey="wizard.reviewStep.cycleNoChip" ns="solutionPreparation" values={{ value: preparation.autoclaveCycleNumber ?? "—" }} components={{ ltr: <Ltr>{null}</Ltr> }} />} />
              </Box>
            ) : (
              <Typography variant="caption" sx={{ color: "error.main", fontStyle: "italic" }}>
                {t("wizard.reviewStep.autoclaveMissing")}
              </Typography>
            )}
          </Box>
        )}

        {/* Components Table */}
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          {t("wizard.reviewStep.componentsHeading")}
        </Typography>

        <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ width: 40, fontWeight: 700 }}>{t("wizard.reviewStep.columns.order")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("wizard.reviewStep.columns.reagentMaterial")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("wizard.reviewStep.columns.recipeTarget")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("wizard.reviewStep.columns.chosenLot")}</TableCell>
                <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("wizard.reviewStep.columns.quantityUsed")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preparation.components.map((c) =>
                c.kind === "Text" ? (
                  <TableRow key={c.id}>
                    <TableCell><Ltr>{c.order}</Ltr></TableCell>
                    <TableCell colSpan={4}>
                      <Typography variant="body2" sx={{ fontStyle: "italic" }}>{c.text}</Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  <TableRow key={c.id}>
                    <TableCell><Ltr>{c.order}</Ltr></TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {c.entryName}
                      </Typography>
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        {c.kind === "PreparedSolution" ? t("wizard.reviewStep.preparedSolution") : <Ltr>{c.entryCode}</Ltr>}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Ltr>{c.recipeQuantity} {c.recipeUnit}</Ltr>
                    </TableCell>
                    <TableCell>
                      {(c.kind === "PreparedSolution" ? c.sourcePreparationCode : c.lotBatchNumber) ? (
                        <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                          <Ltr>{c.kind === "PreparedSolution" ? c.sourcePreparationCode : c.lotBatchNumber}</Ltr>
                        </Typography>
                      ) : (
                        <Typography variant="caption" sx={{ color: "error.main", fontStyle: "italic" }}>
                          {t("wizard.reviewStep.noneSelected")}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell sx={{ textAlign: "right" }}>
                      {c.quantityUsed != null ? (
                        <Typography variant="body2" sx={{ fontWeight: 700 }}>
                          <Ltr>{c.quantityUsed} {c.kind === "PreparedSolution" ? c.recipeUnit ?? "" : c.lotUnit ?? ""}</Ltr>
                        </Typography>
                      ) : (
                        <Typography
                          variant="caption"
                          sx={{ color: c.kind === "PreparedSolution" ? "text.secondary" : "error.main", fontStyle: "italic" }}
                        >
                          {c.kind === "PreparedSolution" ? t("wizard.reviewStep.notRecorded") : t("wizard.reviewStep.noneEntered")}
                        </Typography>
                      )}
                    </TableCell>
                  </TableRow>
                )
              )}
            </TableBody>
          </Table>
        </TableContainer>

        <Box sx={{ display: "flex", justifyContent: "space-between", pt: 1 }}>
          <Button
            variant="outlined"
            onClick={onBack}
            startIcon={<ArrowBackIcon />}
          >
            {t("wizard.reviewStep.backToEdit")}
          </Button>

          <Button
            variant="contained"
            color="primary"
            onClick={onSign}
            disabled={!canProceed}
            startIcon={<LockOutlinedIcon />}
            sx={{ textTransform: "none", fontWeight: 700, px: 3 }}
          >
            {t("wizard.reviewStep.signAndComplete")}
          </Button>
        </Box>
      </Paper>
    </Stack>
  );
}
