import { useMemo } from "react";
import {
  Typography,
  Grid,
  Card,
  Radio,
  RadioGroup,
  FormControlLabel,
  Chip,
  Box,
  Stack,
  Alert,
  useTheme
} from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { ChromatographyColumnDto } from "../../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";
import type { HplcTechnique } from "../types";
import { useTechnique } from "../useTechnique";

export interface WizardColumnStepProps {
  columns: ChromatographyColumnDto[];
  selectedColumnId: number | null;
  equipmentId: number;
  targetDesignation: string;
  technique?: HplcTechnique;
  onSelectColumn: (id: number) => void;
}

export function WizardColumnStep({
  columns,
  selectedColumnId,
  equipmentId,
  targetDesignation,
  technique,
  onSelectColumn
}: WizardColumnStepProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const { technique: routeTechnique } = useTechnique();
  const activeTechnique = technique ?? routeTechnique;

  const qualifyingColumns = useMemo(() => {
    const reqDesig = targetDesignation.trim().toLowerCase();
    return columns.filter((col) => {
      if (col.isActive === false) return false;
      const colDesig = (col.uspDesignation ?? "").trim().toLowerCase();
      if (!reqDesig || colDesig !== reqDesig) return false;
      return col.compatibleEquipment?.some((e) => e.id === equipmentId);
    });
  }, [columns, targetDesignation, equipmentId]);

  const designationLabel = activeTechnique === "Gc" ? t("wizard.columnStep.uspGc") : t("wizard.columnStep.usp");

  return (
    <Stack spacing={2.5}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          {activeTechnique === "Gc" ? t("wizard.columnStep.selectGc") : t("wizard.columnStep.selectHplc")}
        </Typography>
        {targetDesignation && (
          <Chip
            size="small"
            label={t("wizard.columnStep.required", { designation: targetDesignation })}
            color="primary"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        )}
      </Box>

      {qualifyingColumns.length === 0 ? (
        <Alert severity="warning" sx={{ borderRadius: 2 }}>
          {targetDesignation
            ? t("wizard.columnStep.noDesignatedColumn", { designation: targetDesignation, uspLabel: designationLabel })
            : t("wizard.columnStep.noQualifyingColumn", { uspLabel: designationLabel })}
        </Alert>
      ) : (
        <RadioGroup
          value={selectedColumnId ?? ""}
          onChange={(e) => onSelectColumn(Number(e.target.value))}
        >
          <Grid container spacing={2}>
            {qualifyingColumns.map((col) => (
              <Grid size={{ xs: 12, sm: 6 }} key={col.id}>
                <Card
                  variant="outlined"
                  onClick={() => onSelectColumn(col.id)}
                  sx={{
                    p: 1.5,
                    cursor: "pointer",
                    borderRadius: 2,
                    borderColor:
                      selectedColumnId === col.id
                        ? theme.palette.primary.main
                        : theme.palette.divider,
                    backgroundColor:
                      selectedColumnId === col.id
                        ? theme.palette.action.selected
                        : theme.palette.background.paper
                  }}
                >
                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                    <FormControlLabel
                      value={col.id}
                      control={<Radio size="small" />}
                      label={
                        <Box>
                          <Typography variant="body1" sx={{ fontWeight: 700, fontSize: 14 }}>
                            {col.name} (<Ltr>{col.code}</Ltr>)
                          </Typography>
                          {col.serialNumber && (
                            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                              {t("wizard.columnStep.serialNumber", { serial: col.serialNumber })}
                            </Typography>
                          )}
                        </Box>
                      }
                      sx={{ m: 0 }}
                    />
                    <Stack direction="row" spacing={0.75} sx={{ alignItems: "center" }}>
                      {col.uspDesignation && (
                        <Chip
                          size="small"
                          label={<Ltr>USP {col.uspDesignation}</Ltr>}
                          variant="outlined"
                          sx={{ fontWeight: 600, fontSize: 12 }}
                        />
                      )}
                      <StatusBadge status="Suitable" label={t("status.compatible")} />
                    </Stack>
                  </Box>
                </Card>
              </Grid>
            ))}
          </Grid>
        </RadioGroup>
      )}
    </Stack>
  );
}
