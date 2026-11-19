import {
  Box,
  Typography,
  TextField,
  Stack,
  Button,
  IconButton,
  Tooltip,
  Paper
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import BiotechOutlinedIcon from "@mui/icons-material/BiotechOutlined";
import { ImpurityRowState, createImpurityRowState } from "./hplcMethodForm";
import { HplcMethodErrors } from "./hplcMethodValidation";
import { useTranslation } from "../../../../i18n";

export interface HplcImpuritiesSectionProps {
  referenceSolutionPercent: string | number;
  disregardLimitPercent: string | number;
  impurities: ImpurityRowState[];
  errors: HplcMethodErrors;
  onReferenceSolutionPercentChange: (val: string) => void;
  onDisregardLimitPercentChange: (val: string) => void;
  onImpuritiesChange: (impurities: ImpurityRowState[]) => void;
}

export function HplcImpuritiesSection({
  referenceSolutionPercent,
  disregardLimitPercent,
  impurities,
  errors,
  onReferenceSolutionPercentChange,
  onDisregardLimitPercentChange,
  onImpuritiesChange
}: HplcImpuritiesSectionProps) {
  const { t } = useTranslation("masterData");
  const handleAddImpurity = () => {
    onImpuritiesChange([
      ...impurities,
      createImpurityRowState()
    ]);
  };

  const handleRemoveImpurity = (index: number) => {
    onImpuritiesChange(impurities.filter((_, i) => i !== index));
  };

  const handleFieldChange = (index: number, field: keyof ImpurityRowState, val: string) => {
    const next = [...impurities];
    next[index] = { ...next[index], [field]: val };
    onImpuritiesChange(next);
  };

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, mt: 2 }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <div>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
            {t("hplcMethod.impurities.title")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("hplcMethod.impurities.subtitle")}
          </Typography>
        </div>
        <Button
          size="small"
          variant="contained"
          startIcon={<AddIcon />}
          onClick={handleAddImpurity}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {t("hplcMethod.impurities.addImpurity")}
        </Button>
      </Stack>

      {/* Method-level fields: Reference solution % and Disregard limit % */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
          gap: 2,
          mb: 2.5,
          p: 2,
          bgcolor: "action.hover",
          borderRadius: 1.5,
          border: "1px solid",
          borderColor: "divider"
        }}
      >
        <TextField
          size="small"
          label={t("hplcMethod.impurities.refSolutionLabel")}
          type="number"
          value={referenceSolutionPercent}
          onChange={(e) => onReferenceSolutionPercentChange(e.target.value)}
          required
          placeholder={t("hplcMethod.impurities.refSolutionPlaceholder")}
          error={Boolean(errors.referenceSolutionPercent)}
          helperText={
            errors.referenceSolutionPercent ??
            t("hplcMethod.impurities.refSolutionHelper")
          }
          slotProps={{ htmlInput: { min: 0.0001, max: 100, step: "any" } }}
          fullWidth
        />
        <TextField
          size="small"
          label={t("hplcMethod.impurities.disregardLimitLabel")}
          type="number"
          value={disregardLimitPercent}
          onChange={(e) => onDisregardLimitPercentChange(e.target.value)}
          placeholder={t("hplcMethod.impurities.disregardLimitPlaceholder")}
          error={Boolean(errors.disregardLimitPercent)}
          helperText={
            errors.disregardLimitPercent ??
            t("hplcMethod.impurities.disregardLimitHelper")
          }
          slotProps={{ htmlInput: { min: 0, step: "any" } }}
          fullWidth
        />
      </Box>

      {/* Impurities Table / List */}
      <Stack spacing={1.5}>
        {impurities.length === 0 ? (
          <Paper
            elevation={0}
            sx={{
              p: 3,
              textAlign: "center",
              bgcolor: "background.paper",
              border: "1px dashed",
              borderColor: "divider",
              borderRadius: 1.5
            }}
          >
            <BiotechOutlinedIcon sx={{ fontSize: 36, color: "text.disabled", mb: 1 }} />
            <Typography variant="body2" color="text.secondary">
              {t("hplcMethod.impurities.empty")}
            </Typography>
          </Paper>
        ) : (
          impurities.map((imp, idx) => {
            const err = (field: string) => errors[`impurities.${idx}.${field}`];
            const rowKey = imp._clientKey ?? (imp.id != null ? `imp-${imp.id}` : `imp-${idx}`);
            return (
              <Paper
                key={rowKey}
                elevation={0}
                sx={{
                  p: 2,
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1.5,
                  bgcolor: "background.paper"
                }}
              >
                <Stack direction="row" spacing={2} sx={{ alignItems: "flex-start" }}>
                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: { xs: "1fr", sm: "1.5fr 1fr 1fr" },
                      gap: 2,
                      flex: 1
                    }}
                  >
                    <TextField
                      size="small"
                      label={t("hplcMethod.impurities.nameLabel")}
                      placeholder={t("hplcMethod.impurities.namePlaceholder")}
                      value={imp.name}
                      onChange={(e) => handleFieldChange(idx, "name", e.target.value)}
                      required
                      error={Boolean(err("name"))}
                      helperText={err("name")}
                      fullWidth
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.impurities.rrtLabel")}
                      type="number"
                      placeholder={t("hplcMethod.impurities.rrtPlaceholder")}
                      value={imp.relativeRetention}
                      onChange={(e) => handleFieldChange(idx, "relativeRetention", e.target.value)}
                      error={Boolean(err("relativeRetention"))}
                      helperText={err("relativeRetention") ?? t("hplcMethod.impurities.rrtHelper")}
                      slotProps={{ htmlInput: { min: 0.001, step: "any" } }}
                      fullWidth
                    />
                    <TextField
                      size="small"
                      label={t("hplcMethod.impurities.cfLabel")}
                      type="number"
                      placeholder={t("hplcMethod.impurities.cfPlaceholder")}
                      value={imp.correctionFactor}
                      onChange={(e) => handleFieldChange(idx, "correctionFactor", e.target.value)}
                      required
                      error={Boolean(err("correctionFactor"))}
                      helperText={err("correctionFactor") ?? t("hplcMethod.impurities.cfHelper")}
                      slotProps={{ htmlInput: { min: 0.001, step: "any" } }}
                      fullWidth
                    />
                  </Box>

                  <Tooltip title={t("hplcMethod.impurities.removeImpurity")}>
                    <span>
                      <IconButton
                        aria-label={t("hplcMethod.impurities.removeImpurity")}
                        size="small"
                        color="error"
                        onClick={() => handleRemoveImpurity(idx)}
                        sx={{ mt: 0.5 }}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </Stack>
              </Paper>
            );
          })
        )}
      </Stack>
    </Box>
  );
}
