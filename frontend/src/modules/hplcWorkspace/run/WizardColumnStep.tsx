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
  useTheme
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import type { ChromatographyColumnDto } from "../../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";

export interface WizardColumnStepProps {
  columns: ChromatographyColumnDto[];
  selectedColumnId: number | null;
  equipmentId: number;
  onSelectColumn: (id: number) => void;
}

export function WizardColumnStep({
  columns,
  selectedColumnId,
  equipmentId,
  onSelectColumn
}: WizardColumnStepProps) {
  const theme = useTheme();

  const isColumnCompatible = (col: ChromatographyColumnDto) =>
    col.compatibleEquipment?.some((e) => e.id === equipmentId);

  return (
    <Stack spacing={2.5}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        Select Chromatography Column:
      </Typography>
      <RadioGroup
        value={selectedColumnId ?? ""}
        onChange={(e) => onSelectColumn(Number(e.target.value))}
      >
        <Grid container spacing={2}>
          {columns.map((col) => {
            const compatible = isColumnCompatible(col);
            return (
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
                            {col.name} ({col.code})
                          </Typography>
                          {col.serialNumber && (
                            <Typography variant="caption" sx={{ color: "text.secondary" }}>
                              S/N: {col.serialNumber}
                            </Typography>
                          )}
                        </Box>
                      }
                      sx={{ m: 0 }}
                    />
                    {compatible && (
                      <Chip
                        size="small"
                        color="success"
                        icon={<CheckCircleIcon sx={{ fontSize: 14 }} />}
                        label="Compatible"
                        sx={{ fontWeight: 600 }}
                      />
                    )}
                  </Box>
                </Card>
              </Grid>
            );
          })}
        </Grid>
      </RadioGroup>
    </Stack>
  );
}
