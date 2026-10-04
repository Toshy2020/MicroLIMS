import {
  Typography,
  Paper,
  Grid,
  Stack,
  useTheme
} from "@mui/material";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import type { SolutionPreparationListItem } from "../../solutionPreparation/types";
import type { HplcTechnique } from "../types";
import { useTechnique } from "../useTechnique";

export interface WizardReviewStepProps {
  instrumentName?: string;
  instrumentCode?: string;
  method: HplcMethodResponse;
  columnName?: string;
  columnCode?: string;
  availablePreparations: Record<string, SolutionPreparationListItem[]>;
  selectedMobilePhases: Record<string, number>;
  technique?: HplcTechnique;
}

export function WizardReviewStep({
  instrumentName,
  instrumentCode,
  method,
  columnName,
  columnCode,
  availablePreparations,
  selectedMobilePhases,
  technique
}: WizardReviewStepProps) {
  const theme = useTheme();
  const { technique: routeTechnique } = useTechnique();
  const activeTechnique = technique ?? routeTechnique;

  return (
    <Stack spacing={2.5}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        Run Setup Summary:
      </Typography>

      <Paper
        elevation={0}
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Instrument</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>{instrumentName} ({instrumentCode})</Typography>
          </Grid>

          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Method</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>{method.name} ({method.abbreviation})</Typography>
          </Grid>

          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>Column</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>
              {columnName} ({columnCode})
            </Typography>
          </Grid>

          {activeTechnique === "Gc" ? (
            <Grid size={{ xs: 12, sm: 6 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>Carrier Gas & Inlet</Typography>
              <Typography variant="body2">
                <strong>Gas:</strong> {method.carrierGas ?? "—"} ·{" "}
                <strong>Split:</strong> {method.splitRatio != null ? `${method.splitRatio}:1` : "Splitless"}
              </Typography>
            </Grid>
          ) : (
            <Grid size={{ xs: 12, sm: 6 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>Mobile Phases</Typography>
              {method.mobilePhases?.map((mp) => {
                const prepId = selectedMobilePhases[mp.channel];
                const prep = availablePreparations[mp.channel]?.find((p) => p.id === prepId);
                return (
                  <Typography key={mp.channel} variant="body2">
                    <strong>Channel {mp.channel}:</strong> {prep?.code ?? "—"} ({mp.solutionMasterName})
                  </Typography>
                );
              })}
            </Grid>
          )}
        </Grid>
      </Paper>
    </Stack>
  );
}
