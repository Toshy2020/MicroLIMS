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
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import type { ChromatographyColumnDto } from "../../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";

export interface WizardColumnStepProps {
  columns: ChromatographyColumnDto[];
  selectedColumnId: number | null;
  equipmentId: number;
  targetDesignation: string;
  onSelectColumn: (id: number) => void;
}

export function WizardColumnStep({
  columns,
  selectedColumnId,
  equipmentId,
  targetDesignation,
  onSelectColumn
}: WizardColumnStepProps) {
  const theme = useTheme();

  const qualifyingColumns = useMemo(() => {
    const reqDesig = targetDesignation.trim().toLowerCase();
    return columns.filter((col) => {
      if (col.isActive === false) return false;
      const colDesig = (col.uspDesignation ?? "").trim().toLowerCase();
      if (!reqDesig || colDesig !== reqDesig) return false;
      return col.compatibleEquipment?.some((e) => e.id === equipmentId);
    });
  }, [columns, targetDesignation, equipmentId]);

  return (
    <Stack spacing={2.5}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          Select Chromatography Column:
        </Typography>
        {targetDesignation && (
          <Chip
            size="small"
            label={`Required: USP ${targetDesignation}`}
            color="primary"
            variant="outlined"
            sx={{ fontWeight: 600 }}
          />
        )}
      </Box>

      {qualifyingColumns.length === 0 ? (
        <Alert severity="warning" sx={{ borderRadius: 2 }}>
          {targetDesignation
            ? `No ${targetDesignation} column is registered for this instrument - add the USP designation in the Column master.`
            : "No qualifying column is registered for this instrument - add the USP designation in the Column master."}
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
                            {col.name} ({col.code})
                          </Typography>
                          {col.serialNumber && (
                            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                              S/N: {col.serialNumber}
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
                          label={`USP ${col.uspDesignation}`}
                          variant="outlined"
                          sx={{ fontWeight: 600, fontSize: 11 }}
                        />
                      )}
                      <Chip
                        size="small"
                        color="success"
                        icon={<CheckCircleIcon sx={{ fontSize: 14 }} />}
                        label="Compatible"
                        sx={{ fontWeight: 600 }}
                      />
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
