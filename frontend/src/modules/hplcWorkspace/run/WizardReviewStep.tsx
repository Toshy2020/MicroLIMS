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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";

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
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const { technique: routeTechnique } = useTechnique();
  const activeTechnique = technique ?? routeTechnique;

  return (
    <Stack spacing={2.5}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        {t("wizard.reviewStep.title")}
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
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("wizard.reviewStep.instrument")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>{instrumentName} (<Ltr>{instrumentCode}</Ltr>)</Typography>
          </Grid>

          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("wizard.reviewStep.method")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>{method.name} (<Ltr>{method.abbreviation}</Ltr>)</Typography>
          </Grid>

          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("wizard.reviewStep.column")}</Typography>
            <Typography variant="body1" sx={{ fontWeight: 600 }}>
              {columnName} (<Ltr>{columnCode}</Ltr>)
            </Typography>
          </Grid>

          {activeTechnique === "Gc" ? (
            <Grid size={{ xs: 12, sm: 6 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("wizard.reviewStep.carrierGasInlet")}</Typography>
              <Typography variant="body2">
                <strong>{t("wizard.reviewStep.gas")}</strong> {method.carrierGas ?? "—"} ·{" "}
                <strong>{t("wizard.reviewStep.split")}</strong> {method.splitRatio != null ? <Ltr>{`${method.splitRatio}:1`}</Ltr> : t("method.splitless")}
              </Typography>
            </Grid>
          ) : (
            <Grid size={{ xs: 12, sm: 6 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("wizard.reviewStep.mobilePhases")}</Typography>
              {method.mobilePhases?.map((mp) => {
                const prepId = selectedMobilePhases[mp.channel];
                const prep = availablePreparations[mp.channel]?.find((p) => p.id === prepId);
                return (
                  <Typography key={mp.channel} variant="body2">
                    <strong>{t("wizard.reviewStep.channel", { channel: mp.channel })}</strong> <Ltr>{prep?.code ?? "—"}</Ltr> ({mp.solutionMasterName})
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
