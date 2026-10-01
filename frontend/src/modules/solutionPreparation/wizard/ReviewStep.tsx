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
import { tableHeadSx } from "../../../theme";
import type {
  SolutionPreparationResponse,
  RecipeSnapshot
} from "../types";

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
      if (!c.materialId || !c.lotBatchNumber) {
        errors.push(`Component "${c.entryName}" does not have a stock lot selected.`);
      } else if (!c.quantityUsed || c.quantityUsed <= 0) {
        errors.push(`Component "${c.entryName}" requires a quantity used greater than 0.`);
      }
    }

    // Final volume required
    if (!preparation.finalVolumeMl || preparation.finalVolumeMl <= 0) {
      errors.push("Final volume is required and must be greater than 0.");
    }

    // pH check
    if (recipe?.phTarget != null) {
      if (preparation.measuredPh == null) {
        errors.push("Measured pH is required before completion.");
      } else if (recipe.phTolerance != null) {
        const diff = Math.abs(preparation.measuredPh - recipe.phTarget);
        if (diff > recipe.phTolerance) {
          errors.push(
            `Measured pH ${preparation.measuredPh} is outside target ${recipe.phTarget} ± ${recipe.phTolerance}.`
          );
        }
      }
    }

    return errors;
  }, [preparation, recipe]);

  const canProceed = validationErrors.length === 0 && !signing;

  return (
    <Stack spacing={3}>
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
          Step 3: Review Preparation
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 3 }}>
          Review the preparation parameters, components, and consumed lot quantities before electronic signature.
        </Typography>

        {validationErrors.length > 0 ? (
          <Alert severity="error" icon={<ErrorOutlinedIcon />} sx={{ mb: 3 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
              Cannot complete preparation. Please correct the following:
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
            All requirements satisfied. Ready for electronic signature and stock deduction.
          </Alert>
        )}

        {/* General Meta */}
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(4, 1fr)" }, gap: 2, mb: 3 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Solution Recipe
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              {preparation.solutionMasterName}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Type
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.type}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              HPLC Method
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {preparation.hplcMethodAbbreviation || "—"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Laboratory Section
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
            label={`Final Volume: ${preparation.finalVolumeMl ?? "—"} mL`}
            color="primary"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
          {preparation.measuredPh != null && (
            <Chip
              label={`Measured pH: ${preparation.measuredPh}${recipe?.phTarget != null ? ` (Target: ${recipe.phTarget} ± ${recipe.phTolerance ?? 0})` : ""}`}
              color={recipe?.phTarget != null && recipe.phTolerance != null && Math.abs(preparation.measuredPh - recipe.phTarget) > recipe.phTolerance ? "error" : "default"}
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
          {recipe?.shelfLifeValue != null && (
            <Chip
              label={`Shelf Life: ${recipe.shelfLifeValue} ${recipe.shelfLifeUnit}`}
              variant="outlined"
              sx={{ fontWeight: 600 }}
            />
          )}
        </Box>

        {/* Components Table */}
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Reagent Components & Lots
        </Typography>

        <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ width: 40, fontWeight: 700 }}>#</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Reagent / Material</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Recipe Target</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>Chosen Stock Lot</TableCell>
                <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Quantity Used</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preparation.components.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>{c.order}</TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {c.entryName}
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary" }}>
                      {c.entryCode}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {c.recipeQuantity} {c.recipeUnit}
                  </TableCell>
                  <TableCell>
                    {c.lotBatchNumber ? (
                      <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                        {c.lotBatchNumber}
                      </Typography>
                    ) : (
                      <Typography variant="caption" sx={{ color: "error.main", fontStyle: "italic" }}>
                        (None selected)
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ textAlign: "right" }}>
                    {c.quantityUsed != null ? (
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        {c.quantityUsed} {c.lotUnit ?? ""}
                      </Typography>
                    ) : (
                      <Typography variant="caption" sx={{ color: "error.main", fontStyle: "italic" }}>
                        (None entered)
                      </Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        <Box sx={{ display: "flex", justifyContent: "space-between", pt: 1 }}>
          <Button
            variant="outlined"
            onClick={onBack}
            startIcon={<ArrowBackIcon />}
          >
            Back to Edit
          </Button>

          <Button
            variant="contained"
            color="primary"
            onClick={onSign}
            disabled={!canProceed}
            startIcon={<LockOutlinedIcon />}
            sx={{ textTransform: "none", fontWeight: 700, px: 3 }}
          >
            Sign & Complete Preparation
          </Button>
        </Box>
      </Paper>
    </Stack>
  );
}
